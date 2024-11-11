using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Networks;

public class CatletIpManager(
    IStateStore stateStore,
    IIpPoolManager poolManager)
    : BaseIpManager(stateStore, poolManager), ICatletIpManager
{
    public EitherAsync<Error, IPAddress[]> ConfigurePortIps(
        Guid projectId,
        string environment,
        CatletNetworkPort port,
        CatletNetworkConfig networkConfig,
        CancellationToken cancellationToken) =>
        from environmentName in EnvironmentName.NewEither(environment)
            .ToAsync()
        from networkName in Optional(networkConfig.Name)
            .Map(EryphNetworkName.NewEither)
            .IfNone(EryphNetworkName.New(EryphConstants.DefaultNetworkName))
            .ToAsync()
        let subnetName = Optional(networkConfig.SubnetV4?.Name)
            .IfNone(EryphConstants.DefaultSubnetName)
        let ipPoolName = Optional(networkConfig.SubnetV4?.IpPool)
            .IfNone(EryphConstants.DefaultIpPoolName)
        from ipAssignments in _stateStore.For<IpAssignment>().IO.ListAsync(
            new IPAssignmentSpecs.GetByPort(port.Id),
            cancellationToken)
        let validDirectAssignments = ipAssignments
            .Filter(a => a is not IpPoolAssignment && IsValidAssignment(a, networkName, subnetName))
        let validPoolAssignments = ipAssignments
            .OfType<IpPoolAssignment>().ToSeq()
            .Filter(a => IsValidPoolAssignment(a, networkName, subnetName, ipPoolName))
        let invalidAssignments = ipAssignments.Except(validDirectAssignments).Except(validPoolAssignments)
        from _ in invalidAssignments
            .Map(a => _stateStore.For<IpAssignment>().IO.DeleteAsync(a))
            .SequenceSerial()
        from newAssignment in validPoolAssignments.IsEmpty
            ? from assignment in CreateAssignment(projectId, environmentName.Value, port, networkName, subnetName, ipPoolName, cancellationToken)
              select Some(assignment)
            : RightAsync<Error, Option<IpPoolAssignment>>(None)
        select validPoolAssignments.Append(newAssignment).Append(validDirectAssignments)
            .Map(a => IPAddress.Parse(a.IpAddress!))
            .ToArray();

    private EitherAsync<Error, IpPoolAssignment> CreateAssignment(
        Guid projectId,
        string environment,
        CatletNetworkPort port,
        EryphNetworkName networkName,
        string subnetName,
        string ipPoolName,
        CancellationToken cancellationToken) =>
        from network in _stateStore.Read<VirtualNetwork>().IO.GetBySpecAsync(
                new VirtualNetworkSpecs.GetByName(projectId, networkName.Value, environment),
                cancellationToken)
        // It is optional to have an environment specific network. Therefore,
        // we fall back to the network in default environment.
        from validNetwork in network.IsNone && environment != EryphConstants.DefaultEnvironmentName
            ? _stateStore.Read<VirtualNetwork>().IO.GetBySpecAsync(
                    new VirtualNetworkSpecs.GetByName(projectId, networkName.Value, EryphConstants.DefaultEnvironmentName),
                    cancellationToken)
                .Bind(fr => fr.ToEitherAsync(Error.New($"Network {networkName} not found in environment {environment} and default environment.")))
            : network.ToEitherAsync(Error.New($"Environment {environment}: Network {networkName} not found."))

        from subnet in _stateStore.Read<VirtualNetworkSubnet>().IO.GetBySpecAsync(
                new SubnetSpecs.GetByNetwork(validNetwork.Id, subnetName),
                cancellationToken)
        from validSubnet in subnet.ToEitherAsync(
            Error.New($"Environment {environment}: Subnet {subnetName} not found in network {networkName}."))
        from assignment in _poolManager.AcquireIp(validSubnet.Id, ipPoolName, cancellationToken)
        let _ = UpdatePortAssignment(port, assignment)
        select assignment;
    /*
    public EitherAsync<Error, IPAddress[]> ConfigurePortIps2(
        Guid projectId,
        string environment,
        CatletNetworkPort port,
        CatletNetworkConfig networkConfig,
        CancellationToken cancellationToken)
    {
        // TODO Why does this iterate over all ports?
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
                    c))
                .ToEither(f => Error.New(f));

        return
            from portAssignments in getPortAssignments
            from validAssignments in portAssignments.Map(
                    assignment => CheckAssignmentConfigured(assignment, networkConfig).ToAsync())
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
    */
    /*
    private EitherAsync<Error, Option<IpAssignment>> CheckAssignmentConfigured(
        IpAssignment assignment,
        EryphNetworkName configuredNetworkName,
        string configuredSubnetName,
        string configuredPoolName) =>
        from _ in RightAsync<Error, Unit>(unit)
        let networkName = assignment.Subnet switch
        {
            VirtualNetworkSubnet subnet => Some(EryphNetworkName.New(subnet.Network.Name)),
            _ => None
        }
        let subnetName = assignment.Subnet.Name
        let isValid = networkName.Match(
            Some: validNetworkName => assignment switch
            {
                IpPoolAssignment poolAssignment => validNetworkName == configuredNetworkName
                    && subnetName == configuredSubnetName
                    && poolAssignment.Pool.Name == configuredPoolName,
                _ => validNetworkName == configuredNetworkName
                     && subnetName == configuredSubnetName,
            },
            None: () => false)
        from result in isValid
            ? RightAsync<Error, Option<IpAssignment>>(assignment)
            : from _ in _stateStore.For<IpAssignment>().IO.DeleteAsync(assignment)
              select Option<IpAssignment>.None
        select result;
    */

    // TODO Check environment match
    // TODO Check IP address is valid

    private static bool IsValidAssignment(
        IpAssignment assignment,
        EryphNetworkName configuredNetwork,
        string configuredSubnet) =>
        assignment.Subnet is VirtualNetworkSubnet subnet
            && EryphNetworkName.New(subnet.Network.Name) == configuredNetwork
            && subnet.Name == configuredSubnet;

    private static bool IsValidPoolAssignment(
        IpPoolAssignment assignment,
        EryphNetworkName configuredNetwork,
        string configuredSubnet,
        string configuredPool) =>
        IsValidAssignment(assignment, configuredNetwork, configuredSubnet)
            && assignment.Pool.Name == configuredPool;
}
