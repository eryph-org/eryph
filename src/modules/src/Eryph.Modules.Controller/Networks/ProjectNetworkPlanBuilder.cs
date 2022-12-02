using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Dbosoft.OVN;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.VmManagement.Networking.Settings;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Networks;

internal class ProjectNetworkPlanBuilder : IProjectNetworkPlanBuilder
{
    private readonly INetworkProviderManager _networkProviderManager;
    private readonly IIpPoolManager _ipPoolManager;
    private readonly IStateStore _stateStore;

    private record ProviderRouterPortInfo(ProviderRouterPort Port, VirtualNetwork Network, ProviderSubnetInfo Subnet);
    private record ProviderSubnetInfo(ProviderSubnet Subnet, NetworkProviderSubnet Config);

    private static ProviderSubnetInfo EmptyProviderSubnetInfo => new(new ProviderSubnet(), new NetworkProviderSubnet());

    public ProjectNetworkPlanBuilder(
        INetworkProviderManager networkProviderManager, IStateStore stateStore, IIpPoolManager ipPoolManager)
    {
        _networkProviderManager = networkProviderManager;
        _stateStore = stateStore;
        _ipPoolManager = ipPoolManager;
    }

    public EitherAsync<Error, NetworkPlan> GenerateNetworkPlan(Guid projectId, CancellationToken cancellationToken)
    {

        var networkPlan = new NetworkPlan(projectId.ToString());

        return from providerConfig in _networkProviderManager.GetCurrentConfiguration()
            let overLayProviders =
                providerConfig.NetworkProviders
                    .Where(x => x.Type is NetworkProviderType.NatOverLay or NetworkProviderType.Overlay)
                    .ToSeq()

            from networks in GetAllNetworks(projectId, cancellationToken)

            from providerSubnets in GetProviderSubnets(overLayProviders, networks, cancellationToken)
            from providerRouterPorts in GetProviderRouterPorts(networks, providerSubnets, cancellationToken)

            let catletPorts = FindPortsOfType<CatletNetworkPort>(networks)

            let p1 = AddProjectRouterAndPorts(networkPlan, networks)
            let p2 = AddExternalNetSwitches(p1, providerSubnets, overLayProviders)
            let p3 = AddProviderRouterPorts(p2, providerRouterPorts)
            let p4 = AddNetworksAsSwitches(p3, networks)
            let p5 = AddSubnetsAsDhcpOptions(p4, networks)
            let p6 = AddCatletPorts(p5, catletPorts)

            select p6;

    }



    private EitherAsync<Error, Seq<ProviderRouterPortInfo>> GetProviderRouterPorts(
        Seq<VirtualNetwork> networks, Seq<ProviderSubnetInfo> providerSubnets, 
        CancellationToken cancellationToken) =>
        networks.Map(network => FindPortsOfType<ProviderRouterPort>(network.NetworkPorts)

                // ensure that every port has a ip address
                .Map(port => port.IpAssignments.Count == 0
                    ? AcquireIpAddress(port, network, providerSubnets, cancellationToken)
                        .Map(p => new ProviderRouterPortInfo(p, network, EmptyProviderSubnetInfo))
                    : new ProviderRouterPortInfo(port, network, EmptyProviderSubnetInfo)))
            .Flatten().TraverseSerial(l => l)

            // find and add provider subnet 
            .Bind(infoSeq => infoSeq.Map(info => providerSubnets
                .Find(x => x.Subnet.ProviderName == info.Network.NetworkProvider && x.Subnet.Name == info.Port.SubnetName)
                .ToEitherAsync(Error.New(
                    $"Network '{info.Network.Name}' configuration error: subnet {info.Port} of network provider {info.Network.NetworkProvider} not found."))
                .Map(providerSubnet => info with { Subnet = providerSubnet })).TraverseParallel(l => l));


    private EitherAsync<Error, Seq<ProviderSubnetInfo>> GetProviderSubnets(
        IEnumerable<NetworkProvider> overLayProviders, Seq<VirtualNetwork> networks, CancellationToken cancellationToken)
    {
        return networks.Map(
                network => network.NetworkPorts.Filter(x => x is ProviderRouterPort)
                    .Select(x => (network.NetworkProvider, ((ProviderRouterPort)x).SubnetName))
                    .Map(rs =>
                        overLayProviders.Find(x => x.Name == rs.NetworkProvider)
                            .ToEither(Error.New(
                                $"Network '{network.Name}' configuration error: network provider {rs.NetworkProvider} not found."))
                            .Bind(provider => provider.Subnets.Find(s => s.Name == rs.SubnetName)
                                .ToEither(Error.New(
                                    $"Network '{network.Name}' configuration error: subnet {rs.SubnetName} not found in network provider {rs.NetworkProvider}.")))
                            .Map(config => (rs.NetworkProvider, rs.SubnetName, Config: config))
                    ).ToSeq()
            ).Flatten().Traverse(l => l).ToAsync()
            .Bind(rs => 
                //get all provider configurations and filter for configured providers
                _stateStore.For<ProviderSubnet>().IO
                .ListAsync(new NetplanBuilderSpecs.GetAllProviderSubnets(), cancellationToken)
                .Map(all => all.Where(a =>
                    rs.Any(r => r.NetworkProvider == a.ProviderName && r.SubnetName == a.Name)))
                .Map(subnets =>

                    //map subnets to ProviderSubnetInfo and add config
                    subnets.Map(subnet => new ProviderSubnetInfo(
                        subnet,
                        rs.First(x =>
                            x.NetworkProvider == subnet.ProviderName && x.SubnetName == subnet.Name).Config))));

    }

    private EitherAsync<Error, Seq<VirtualNetwork>> GetAllNetworks(Guid projectId, CancellationToken cancellationToken) =>
        _stateStore.For<VirtualNetwork>().IO.ListAsync(new NetplanBuilderSpecs.GetAllNetworks(projectId), cancellationToken);


    private static NetworkPlan AddProviderRouterPorts(NetworkPlan networkPlan, Seq<ProviderRouterPortInfo> ports)
    {
        return ports.Map(portInfo =>
            {
                var externalIpAddress = portInfo.Port.IpAssignments.First().IpAddress.Apply(IPAddress.Parse);
                var externalNetwork = IPNetwork.Parse(portInfo.Subnet.Subnet.IpNetwork);

                return networkPlan.AddRouterPort($"externalNet-{networkPlan.Id}-{portInfo.Network.NetworkProvider}",
                        $"project-{networkPlan.Id}",
                        portInfo.Port.MacAddress, externalIpAddress, externalNetwork, "local")

                    .AddNATRule($"project-{networkPlan.Id}", "snat",
                        externalIpAddress, "", portInfo.Network.IpNetwork)

                    .AddStaticRoute($"project-{networkPlan.Id}", "0.0.0.0/0",
                        IPAddress.Parse(portInfo.Subnet.Config.Gateway));

            })
            .Apply(s => JoinPlans(s, networkPlan));

    }

    private static NetworkPlan AddExternalNetSwitches(NetworkPlan networkPlan, 
        Seq<ProviderSubnetInfo> subnets, Seq<NetworkProvider> overlayProviders)
    {
        return (from providerNames in subnets.Select(x => x.Subnet.ProviderName).Distinct()
            from provider in overlayProviders.Where(p => providerNames.Contains(p.Name))
            let p1 = networkPlan.AddSwitch($"externalNet-{networkPlan.Id}-{provider.Name}")
            let p2 = p1.AddExternalNetworkPort($"externalNet-{networkPlan.Id}-{provider.Name}",
                provider.Name)

            select p2).Apply(s => JoinPlans(s, networkPlan));
    }


    private static NetworkPlan AddNetworksAsSwitches(NetworkPlan networkPlan, Seq<VirtualNetwork> networks) =>
        networks.Map(network => networkPlan.AddSwitch(network.Id.ToString()))
            .Apply(s => JoinPlans(s, networkPlan));


    private static NetworkPlan AddSubnetsAsDhcpOptions(NetworkPlan networkPlan, Seq<VirtualNetwork> networks) =>
        (from network in networks
            let networkIp = network.RouterPort.IpAssignments.First()
         from subnet in network.Subnets
            let p1 = networkPlan.AddDHCPOptions(
                subnet.Id.ToString(), IPNetwork.Parse(subnet.IpNetwork),
                new Map<string, string>(
                    new []
                    {
                        ("server_id", networkIp.IpAddress ),
                        ("server_mac", network.RouterPort.MacAddress ),
                        ("lease_time", subnet.DhcpLeaseTime == 0 ? "3600" : subnet.DhcpLeaseTime.ToString() ),
                        ("mtu", subnet.MTU == 0 ? "1400" : subnet.MTU.ToString() ),
                        ("dns_server", string.IsNullOrWhiteSpace(subnet.DnsServersV4) ? "9.9.9.9" : subnet.DnsServersV4 ),
                        ("router", networkIp.IpAddress )

                    }
                )
            )
            select p1)
        .Apply(s => JoinPlans(s.ToSeq(), networkPlan));


    private static NetworkPlan AddCatletPorts(NetworkPlan networkPlan, Seq<CatletNetworkPort> ports) =>
        ports.Map(port => networkPlan.AddNetworkPort(
                port.Network.Id.ToString(), port.Name, port.MacAddress,
                port.IpAssignments.HeadOrNone().Map(h => IPAddress.Parse(h.IpAddress))
                    .IfNone(IPAddress.None), 
                        port.IpAssignments?.FirstOrDefault()?.SubnetId?.ToString() ?? ""))
            .Apply(s => JoinPlans(s, networkPlan));


    private static NetworkPlan AddProjectRouterAndPorts(NetworkPlan networkPlan, Seq<VirtualNetwork> networks)
    {
        networkPlan = networkPlan.AddRouter($"project-{networkPlan.Id}");
        return networks.Map(network =>
        {
            var ipNetwork = IPNetwork.Parse(network.IpNetwork);
            return networkPlan.AddRouterPort(network.Id.ToString(),
                $"project-{networkPlan.Id}", network.RouterPort.MacAddress, 
                network.RouterPort.IpAssignments.First().IpAddress.Apply(IPAddress.Parse), ipNetwork);
        }).Apply(s => JoinPlans(s, networkPlan));

    }

    private static Seq<T> FindPortsOfType<T>(Seq<VirtualNetwork> networks) where T : VirtualNetworkPort
    {
        return networks.SelectMany(x => x.NetworkPorts)
            .Apply(FindPortsOfType<T>);

    }

    private static Seq<T> FindPortsOfType<T>(IEnumerable<VirtualNetworkPort> ports) where T : VirtualNetworkPort
    {
        return ports.Where(p => p is T).Cast<T>()
            .ToSeq();

    }

    private EitherAsync<Error, ProviderRouterPort> AcquireIpAddress(ProviderRouterPort port, VirtualNetwork network,
        Seq<ProviderSubnetInfo> providerSubnets, CancellationToken cancellationToken)
    {
        return from providerSubnet in providerSubnets
                .Find(x => x.Subnet.ProviderName == network.NetworkProvider && x.Subnet.Name == port.SubnetName)
                .ToEitherAsync(Error.New(
                    $"Network '{network.Name}' configuration error: subnet {port.SubnetName} of network provider {network.NetworkProvider} not found."))
            from ip in _ipPoolManager.AcquireIp(providerSubnet.Subnet.Id, port.PoolName, cancellationToken)
            let _ = AddIpToPort(port, ip)
            select  port;
    }

    private static NetworkPort AddIpToPort(NetworkPort port, IpAssignment ipAssignment)
    {
        port.IpAssignments ??= new List<IpAssignment>();
        port.IpAssignments.Add(ipAssignment);
        return port;
    }



    private static NetworkPlan JoinPlans(Seq<NetworkPlan> plans, NetworkPlan basePlan)
    {
        var result = new NetworkPlan(basePlan.Id);

        return Enumerable.Aggregate(plans.Add(basePlan), result, (current, networkPlan) => current with
        {
            PlannedDHCPOptions = current.PlannedDHCPOptions + networkPlan.PlannedDHCPOptions,
            PlannedDnsRecords = current.PlannedDnsRecords + networkPlan.PlannedDnsRecords,
            PlannedNATRules = current.PlannedNATRules + networkPlan.PlannedNATRules,
            PlannedRouterPorts = current.PlannedRouterPorts + networkPlan.PlannedRouterPorts,
            PlannedRouterStaticRoutes = current.PlannedRouterStaticRoutes + networkPlan.PlannedRouterStaticRoutes,
            PlannedRouters = current.PlannedRouters + networkPlan.PlannedRouters,
            PlannedSwitchPorts = current.PlannedSwitchPorts + networkPlan.PlannedSwitchPorts,
            PlannedSwitches = current.PlannedSwitches + networkPlan.PlannedSwitches,
        });
    }
}