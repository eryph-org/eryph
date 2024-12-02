using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
    : ICatletIpManager
{
    public EitherAsync<Error, Seq<IPAddress>> ConfigurePortIps(
        VirtualNetwork network,
        CatletNetworkPort port,
        CatletNetworkConfig networkConfig) =>
        from _ in RightAsync<Error, Unit>(unit)
        let subnetName = Optional(networkConfig.SubnetV4?.Name)
            .IfNone(EryphConstants.DefaultSubnetName)
        let ipPoolName = Optional(networkConfig.SubnetV4?.IpPool)
            .IfNone(EryphConstants.DefaultIpPoolName)
        from ipAssignments in stateStore.For<IpAssignment>().IO.ListAsync(
            new IPAssignmentSpecs.GetByPortWithPoolAndSubnet(port.Id))
        let validDirectAssignments = ipAssignments
            .Filter(a => a is not IpPoolAssignment && IsValidAssignment(a, network, subnetName))
        let validPoolAssignments = ipAssignments
            .OfType<IpPoolAssignment>().ToSeq()
            .Filter(a => IsValidPoolAssignment(a, network, subnetName, ipPoolName))
        let invalidAssignments = ipAssignments.Except(validDirectAssignments).Except(validPoolAssignments)
        from __ in invalidAssignments
            .Map(a => stateStore.For<IpAssignment>().IO.DeleteAsync(a))
            .SequenceSerial()
        from newAssignment in validPoolAssignments.IsEmpty
            ? from assignment in CreateAssignment(network, port, subnetName, ipPoolName)
              select Some(assignment)
            : RightAsync<Error, Option<IpPoolAssignment>>(None)
        select validPoolAssignments.Append(newAssignment).Append(validDirectAssignments)
            .Map(a => IPAddress.Parse(a.IpAddress!))
            .ToSeq();

    private EitherAsync<Error, IpPoolAssignment> CreateAssignment(
        VirtualNetwork network,
        CatletNetworkPort port,
        string subnetName,
        string ipPoolName) =>
        from subnet in stateStore.For<VirtualNetworkSubnet>().IO.GetBySpecAsync(
            new SubnetSpecs.GetByNetwork(network.Id, subnetName))
        from validSubnet in subnet.ToEitherAsync(
            Error.New($"Environment {network.Environment}: Subnet {subnetName} not found in network {network.Name}."))
        from assignment in poolManager.AcquireIp(validSubnet.Id, ipPoolName)
        let _ = fun(() => { assignment.NetworkPort = port; })()
        select assignment;

    private static bool IsValidAssignment(
        IpAssignment assignment,
        VirtualNetwork network,
        string configuredSubnet) =>
        assignment.Subnet is VirtualNetworkSubnet subnet
            && subnet.NetworkId == network.Id
            && subnet.Name == configuredSubnet;

    private static bool IsValidPoolAssignment(
        IpPoolAssignment assignment,
        VirtualNetwork network,
        string configuredSubnet,
        string configuredPool) =>
        IsValidAssignment(assignment, network, configuredSubnet)
            && assignment.Pool.Name == configuredPool;
}
