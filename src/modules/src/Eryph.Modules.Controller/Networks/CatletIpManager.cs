﻿using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Networks;

public class CatletIpManager : BaseIpManager, ICatletIpManager
{

    public CatletIpManager(IStateStore stateStore, IIpPoolManager poolManager): base(stateStore, poolManager)
    {
    }


    public EitherAsync<Error, IPAddress[]> ConfigurePortIps(
        Guid projectId,
        string environment,
        CatletNetworkPort port,
        CatletNetworkConfig[] networkConfigs, CancellationToken cancellationToken)
    {

        var portNetworks = networkConfigs.Map(x =>
            new PortNetwork(x.Name, 
                x.SubnetV4 == null 
                    ? Option<string>.None
                : x.SubnetV4.Name , 
                x.SubnetV4 == null 
                    ? Option<string>.None 
                    : x.SubnetV4.IpPool));

        var getPortAssignments =
            Prelude.TryAsync(_stateStore.For<IpAssignment>().ListAsync(new IPAssignmentSpecs.GetByPort(port.Id),
                    cancellationToken))
                .ToEither(f => Error.New(f));

        return
            from portAssignments in getPortAssignments
            from validAssignments in portAssignments.Map(
                    assignment => CheckAssignmentConfigured(assignment, networkConfigs).ToAsync())
                .TraverseSerial(l => l.AsEnumerable())
                .Map(e => e.Flatten())

            from validAndNewAssignments in portNetworks.Map(portNetwork =>
            {
                var networkNameString = portNetwork.NetworkName.IfNone("default");
                var subnetNameString = portNetwork.SubnetName.IfNone("default");
                var poolNameString = portNetwork.PoolName.IfNone("default");
                return
                    from network in _stateStore.Read<VirtualNetwork>()
                        .IO.GetBySpecAsync(new VirtualNetworkSpecs.GetByName(projectId, networkNameString,environment), cancellationToken)
                        .Bind(r =>

                            // it is optional to have a environment specific network
                            // therefore fallback to network in default environment if not found
                            r.IsNone && environment != "default" ?
                                _stateStore.Read<VirtualNetwork>()
                            .IO.GetBySpecAsync(new VirtualNetworkSpecs.GetByName(projectId, networkNameString, "default"), cancellationToken)
                            .Bind(fr => fr.ToEitherAsync(Error.New($"Network {networkNameString} not found in environment {environment} and default environment.")))
                            :  r.ToEitherAsync(Error.New($"Environment {environment}: Network {networkNameString} not found.")))

                    from subnet in _stateStore.Read<VirtualNetworkSubnet>().IO
                        .GetBySpecAsync(new SubnetSpecs.GetByNetwork(network.Id, subnetNameString), cancellationToken)
                        .Bind(r => r.ToEitherAsync(
                            Error.New($"Environment {environment}: Subnet {subnetNameString} not found in network {networkNameString}.")))

                    let existingAssignment = CheckAssignmentExist(validAssignments, subnet, poolNameString)

                    from assignment in existingAssignment.IsSome ?
                        existingAssignment.ToEitherAsync(Errors.None)
                        : from newAssignment in _poolManager.AcquireIp(subnet.Id, poolNameString, cancellationToken)
                            .Map(a => (IpAssignment)a)
                          let _ = UpdatePortAssignment(port, newAssignment)
                          select newAssignment
                    select assignment;

            }).SequenceSerial()

            select validAndNewAssignments
                .Select(x => IPAddress.Parse(x.IpAddress)).ToArray();

    }

    private async Task<Either<Error, Option<IpAssignment>>> CheckAssignmentConfigured(IpAssignment assignment, CatletNetworkConfig[] networkConfigs)
    {
        var networkName = "";
        var poolName = "";

        await _stateStore.LoadPropertyAsync(assignment, x => x.Subnet);
        if (assignment.Subnet is VirtualNetworkSubnet networkSubnet)
        {
            await _stateStore.LoadPropertyAsync(networkSubnet, x => x.Network);
            networkName = networkSubnet.Network.Name;
        }

        if (assignment is IpPoolAssignment poolAssignment)
        {
            await _stateStore.LoadPropertyAsync(poolAssignment, x => x.Pool);
            poolName = poolAssignment.Pool.Name;
        }

        if (networkConfigs.Any(x => x.Name == networkName 
              && (string.IsNullOrWhiteSpace(poolName) || poolName == (x.SubnetV4?.IpPool?? "default") )))
            return Prelude.Right<Error, Option<IpAssignment>>(assignment);

        // remove invalid
        await _stateStore.For<IpAssignment>().DeleteAsync(assignment);
        return Prelude.Right<Error, Option<IpAssignment>>(Option<IpAssignment>.None);

    }


    private record PortNetwork(
        Option<string> NetworkName,
        Option<string> SubnetName,
        Option<string> PoolName);

}