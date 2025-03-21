﻿using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Networks;

internal class ProviderIpManager(
    IStateStore stateStore,
    IIpPoolManager poolManager)
    : IProviderIpManager
{
    public EitherAsync<Error, Seq<IPAddress>> ConfigureFloatingPortIps(
        string providerName,
        FloatingNetworkPort port) =>
        from ipAssignments in stateStore.For<IpAssignment>().IO.ListAsync(
            new IPAssignmentSpecs.GetByPortWithPoolAndSubnet(port.Id))
        let validDirectAssignments = ipAssignments
            .Filter(a => a is not IpPoolAssignment && IsValidAssignment(a, providerName, port.SubnetName))
        let validPoolAssignments = ipAssignments
            .OfType<IpPoolAssignment>().ToSeq()
            .Filter(a => IsValidPoolAssignment(a, providerName, port.SubnetName, port.PoolName))
        let invalidAssignments = ipAssignments.Except(validDirectAssignments).Except(validPoolAssignments)
        from _ in invalidAssignments
            .Map(a => stateStore.For<IpAssignment>().IO.DeleteAsync(a))
            .SequenceSerial()
        from newAssignment in validPoolAssignments.IsEmpty
            ? from assignment in CreateAssignment(port, providerName, port.SubnetName, port.PoolName)
              select Some(assignment)
            : RightAsync<Error, Option<IpPoolAssignment>>(None)
        select validPoolAssignments.Append(newAssignment).Append(validDirectAssignments)
            .Map(a => IPAddress.Parse(a.IpAddress!))
            .ToSeq();

    private EitherAsync<Error, IpPoolAssignment> CreateAssignment(
        FloatingNetworkPort port,
        string providerName,
        string subnetName,
        string ipPoolName) =>
        from subnet in stateStore.For<ProviderSubnet>().IO.GetBySpecAsync(
            new SubnetSpecs.GetByProviderName(providerName, subnetName))
        from validSubnet in subnet.ToEitherAsync(
            Error.New($"Subnet {subnetName} not found for provider {providerName}."))
        from assignment in poolManager.AcquireIp(validSubnet.Id, ipPoolName)
        let _ = fun(() => { assignment.NetworkPort = port; })()
        select assignment;

    private static bool IsValidAssignment(
        IpAssignment assignment,
        string configuredProvider,
        string configuredSubnet) =>
        assignment.Subnet is ProviderSubnet subnet
        && subnet.ProviderName == configuredProvider
        && subnet.Name == configuredSubnet;

    private static bool IsValidPoolAssignment(
        IpPoolAssignment assignment,
        string configuredProvider,
        string configuredSubnet,
        string configuredPool) =>
        IsValidAssignment(assignment, configuredProvider, configuredSubnet)
        && assignment.Pool.Name == configuredPool;
}
