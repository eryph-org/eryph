using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Eryph.ConfigModel.Machine;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Networks;

internal class CatletIpManager : ICatletIpManager
{
    private readonly IStateStore _stateStore;
    private readonly IIpPoolManager _poolManager;

    public CatletIpManager(IStateStore stateStore, IIpPoolManager poolManager)
    {
        _stateStore = stateStore;
        _poolManager = poolManager;
    }


    public EitherAsync<Error, IPAddress[]> ConfigurePortIps(
        Guid projectId,
        CatletNetworkPort port,
        MachineNetworkConfig[] networkConfigs, CancellationToken cancellationToken)
    {

        var portNetworks = networkConfigs.Map(x =>
            new PortNetwork(x.Name, Option<string>.None, Option<string>.None));

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
                        .IO.GetBySpecAsync(new VirtualNetworkSpecs.GetByName(projectId, networkNameString), cancellationToken)
                        .Bind(r => r.ToEitherAsync(Error.New($"Network {networkNameString} not found.")))

                    from subnet in _stateStore.Read<VirtualNetworkSubnet>().IO
                        .GetBySpecAsync(new SubnetSpecs.GetByNetwork(network.Id, subnetNameString), cancellationToken)
                        .Bind(r => r.ToEitherAsync(
                            Error.New($"Subnet {subnetNameString} not found in network {networkNameString}.")))

                    let existingAssignment = CheckAssignmentExist(validAssignments, subnet, poolNameString)

                    from assignment in existingAssignment.IsSome ?
                        existingAssignment.ToEitherAsync(Errors.None)
                        : from newAssignment in _poolManager.AcquireIp(subnet.Id, poolNameString, cancellationToken)
                            .Map(a => (IpAssignment)a)
                          let _ = UpdatePortAssignment(port, newAssignment)
                          select newAssignment
                    select assignment;

            }).TraverseParallel(l => l)

            select validAndNewAssignments
                .Select(x => IPAddress.Parse(x.IpAddress)).ToArray();

    }

    private static Unit UpdatePortAssignment(NetworkPort port, IpAssignment newAssignment)
    {
        newAssignment.NetworkPortId = port.Id;
        
        return Unit.Default;
    }

    private static Option<IpAssignment> CheckAssignmentExist(
        IEnumerable<IpAssignment> validAssignments,
        Subnet subnet, string poolName)
    {
        return validAssignments.Find(x => x.Subnet.Id == subnet.Id)
            .Bind(s =>
            {
                if (s is not IpPoolAssignment poolAssignment)
                    return s;

                return poolAssignment.Pool.Name == poolName
                    ? s
                    : Option<IpAssignment>.None;
            });
    }

    private async Task<Either<Error, Option<IpAssignment>>> CheckAssignmentConfigured(IpAssignment assignment, MachineNetworkConfig[] networkConfigs)
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

        if (networkConfigs.Any(x => x.Name == networkName))
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

//internal class ProviderIpManager
//{
//    private readonly IStateStore _stateStore;
//    private readonly IIpPoolManager _poolManager;

//    public ProviderIpManager(IStateStore stateStore, IIpPoolManager poolManager)
//    {
//        _stateStore = stateStore;
//        _poolManager = poolManager;
//    }


//    public EitherAsync<Error, IPAddress[]> ConfigurePortIps(
//        ProviderNetworkPort port,

//        CancellationToken cancellationToken)
//    {

//        var getPortAssignments =
//            Prelude.TryAsync(_stateStore.For<IpAssignment>().ListAsync(new IPAssignmentSpecs.GetByPort(port.Id),
//                    cancellationToken))
//                .ToEither(f => Error.New(f));

//        return
//            from portAssignments in getPortAssignments
//            from validAssignments in portAssignments.Map(
//                    assignment => CheckAssignmentConfigured(assignment, networkConfigs).ToAsync())
//                .TraverseSerial(l => l.AsEnumerable())
//                .Map(e => e.Flatten())

//            from validAndNewAssignments in portNetworks.Map(portNetwork =>
//            {
//                var networkNameString = portNetwork.NetworkName.IfNone("default");
//                var subnetNameString = portNetwork.SubnetName.IfNone("default");
//                var poolNameString = portNetwork.PoolName.IfNone("default");

//                return
//                    from network in _stateStore.Read<VirtualNetwork>()
//                        .IO.GetBySpecAsync(new VirtualNetworkSpecs.GetByName(projectId, networkNameString), cancellationToken)
//                        .Bind(r => r.ToEitherAsync(Error.New($"Network {networkNameString} not found.")))

//                    from subnet in _stateStore.Read<VirtualNetworkSubnet>().IO
//                        .GetBySpecAsync(new SubnetSpecs.GetByNetwork(network.Id, subnetNameString), cancellationToken)
//                        .Bind(r => r.ToEitherAsync(
//                            Error.New($"Subnet {subnetNameString} not found in network {networkNameString}.")))

//                    let existingAssignment = CheckAssignmentExist(validAssignments, subnet, poolNameString)

//                    from assignment in existingAssignment.IsSome ?
//                        existingAssignment.ToEitherAsync(Errors.None)
//                        : from newAssignment in _poolManager.AcquireIp(subnet.Id, poolNameString, cancellationToken)
//                            .Map(a => (IpAssignment)a)
//                          let _ = UpdatePortAssignment(port, newAssignment)
//                          select newAssignment
//                    select assignment;

//            }).TraverseParallel(l => l)

//            select validAndNewAssignments
//                .Select(x => IPAddress.Parse(x.IpAddress)).ToArray();

//    }

//    private static Unit UpdatePortAssignment(VirtualNetworkPort port, IpAssignment newAssignment)
//    {
//        newAssignment.NetworkPortId = port.Id;

//        return Unit.Default;
//    }

//    private static Option<IpAssignment> CheckAssignmentExist(
//        IEnumerable<IpAssignment> validAssignments,
//        Subnet subnet, string poolName)
//    {
//        return validAssignments.Find(x => x.Subnet.Id == subnet.Id)
//            .Bind(s =>
//            {
//                if (s is not IpPoolAssignment poolAssignment)
//                    return s;

//                return poolAssignment.Pool.Name == poolName
//                    ? s
//                    : Option<IpAssignment>.None;
//            });
//    }

//    private async Task<Either<Error, Option<IpAssignment>>> CheckAssignmentConfigured(IpAssignment assignment, MachineNetworkConfig[] networkConfigs)
//    {
//        var networkName = "";
//        var poolName = "";

//        await _stateStore.LoadPropertyAsync(assignment, x => x.Subnet);
//        if (assignment.Subnet is VirtualNetworkSubnet networkSubnet)
//        {
//            await _stateStore.LoadPropertyAsync(networkSubnet, x => x.Network);
//            networkName = networkSubnet.Network.Name;
//        }

//        if (assignment is IpPoolAssignment poolAssignment)
//        {
//            await _stateStore.LoadPropertyAsync(poolAssignment, x => x.Pool);
//            poolName = poolAssignment.Pool.Name;
//        }

//        if (networkConfigs.Any(x => x.Name == networkName))
//            return Prelude.Right<Error, Option<IpAssignment>>(assignment);

//        // remove invalid
//        await _stateStore.For<IpAssignment>().DeleteAsync(assignment);
//        return Prelude.Right<Error, Option<IpAssignment>>(Option<IpAssignment>.None);

//    }

//    private record PortNetwork(
//        Option<string> NetworkName,
//        Option<string> SubnetName,
//        Option<string> PoolName);
//}