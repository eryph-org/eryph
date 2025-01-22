using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Dbosoft.OVN;
using Dbosoft.OVN.Model.OVN;
using Eryph.Core.Network;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Networks;

internal class ProjectNetworkPlanBuilder(
    IStateStore stateStore)
    : IProjectNetworkPlanBuilder
{
    private sealed record FloatingPortInfo(FloatingNetworkPort Port);

    private sealed record ProviderRouterPortInfo(ProviderRouterPort Port, VirtualNetwork Network, ProviderSubnetInfo Subnet);
    
    private sealed record ProviderSubnetInfo(ProviderSubnet Subnet, NetworkProviderSubnet Config);

    private sealed record CatletDnsInfo(string CatletMetadataId, Map<string, string> Addresses);

    public EitherAsync<Error, NetworkPlan> GenerateNetworkPlan(
        Guid projectId,
        NetworkProvidersConfiguration providerConfig) =>
        from _ in RightAsync<Error, Unit>(unit)
        let networkPlan = new NetworkPlan(projectId.ToString())
        let overLayProviders =
            providerConfig.NetworkProviders
                .Where(x => x.Type is NetworkProviderType.NatOverLay or NetworkProviderType.Overlay)
                .ToSeq()

        from networks in GetAllOverlayNetworks(projectId, overLayProviders)

        from providerSubnets in GetProviderSubnets(overLayProviders, networks)
        from providerRouterPorts in GetProviderRouterPorts(networks, providerSubnets)

        let catletPorts = FindPortsOfType<CatletNetworkPort>(networks)
        from floatingPorts in GetFloatingPorts(catletPorts, providerSubnets)
        let dnsInfo = MapCatletPortsToDnsInfos(catletPorts)
        let dnsNames = dnsInfo.Select(info => info.CatletMetadataId).ToArray()

        let p1 = AddProjectRouterAndPorts(networkPlan, networks)
        from p2 in AddExternalNetSwitches(p1, providerSubnets, overLayProviders)
        from p3 in AddProviderRouterPorts(p2, providerRouterPorts)
        let p4 = AddNetworksAsSwitches(p3, networks, dnsNames)
        let p5 = AddSubnetsAsDhcpOptions(p4, networks)
        let p6 = AddCatletPorts(p5, catletPorts)
        let p7 = AddFloatingPorts(p6, floatingPorts)
        let p8 = AddDnsNames(p7, dnsInfo)
        select p8;
    

    private EitherAsync<Error, Seq<ProviderRouterPortInfo>> GetProviderRouterPorts(
        Seq<VirtualNetwork> networks,
        Seq<ProviderSubnetInfo> providerSubnets) =>
        from _ in RightAsync<Error, Unit>(unit)
        let portInfos = networks.SelectMany(
                network => FindPortsOfType<ProviderRouterPort>(network.NetworkPorts),
                (network, port) => (Network: network, Port: port))
        // find and add provider subnet 
        from portInfos2 in portInfos.Map(portInfo => providerSubnets
                .Find(x => x.Subnet.ProviderName == portInfo.Network.NetworkProvider && x.Subnet.Name == portInfo.Port.SubnetName)
                .ToEitherAsync(Error.New(
                    $"Network '{portInfo.Network.Name}' configuration error: subnet {portInfo.Port.SubnetName} of network provider {portInfo.Network.NetworkProvider} not found."))
                .Map(providerSubnet => new ProviderRouterPortInfo(portInfo.Port, portInfo.Network, providerSubnet)))
            .SequenceSerial()
        select portInfos2;

    private static EitherAsync<Error, Seq<FloatingPortInfo>> GetFloatingPorts(
        Seq<CatletNetworkPort> catletPorts, Seq<ProviderSubnetInfo> providerSubnets)
    {
        return catletPorts
            .Filter(x => x.FloatingPort != null && x.IpAssignments.Count > 0)
            .Map(x => new FloatingPortInfo(x.FloatingPort))
            // find and add provider subnet 
            .Map(info => providerSubnets
                .Find(x => x.Subnet.ProviderName == info.Port.ProviderName &&
                           x.Subnet.Name == info.Port.SubnetName)
                .ToEitherAsync(Error.New(
                    $"Port '{info.Port.Name}' configuration error: subnet {info.Port.SubnetName} of network provider {info.Port.ProviderName} not found."))
                .Map(_ => info )).SequenceSerial();
    }


    private EitherAsync<Error, Seq<ProviderSubnetInfo>> GetProviderSubnets(
        Seq<NetworkProvider> overLayProviders,
        Seq<VirtualNetwork> networks)
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
            ).Flatten().Sequence().ToAsync()
            .Bind(rs => 
                //get all provider configurations and filter for configured providers
                stateStore.For<ProviderSubnet>().IO
                .ListAsync(new NetplanBuilderSpecs.GetAllProviderSubnets())
                .Map(all => all.Where(a =>
                    rs.Any(r => r.NetworkProvider == a.ProviderName && r.SubnetName == a.Name)))
                .Map(subnets =>
                    //map subnets to ProviderSubnetInfo and add config
                    subnets.Map(subnet => new ProviderSubnetInfo(
                        subnet,
                        rs.First(x =>
                            x.NetworkProvider == subnet.ProviderName && x.SubnetName == subnet.Name).Config))));

    }

    private EitherAsync<Error, Seq<VirtualNetwork>> GetAllOverlayNetworks(
        Guid projectId,
        Seq<NetworkProvider> overLayProviders) =>
        stateStore.For<VirtualNetwork>().IO
            .ListAsync(new NetplanBuilderSpecs.GetAllNetworks(projectId))
            .Map(s => s.Filter(x => overLayProviders.Any(p => p.Name == x.NetworkProvider)));


    private static EitherAsync<Error, NetworkPlan> AddProviderRouterPorts(
        NetworkPlan networkPlan,
        Seq<ProviderRouterPortInfo> ports) =>
        from plans in ports.Map(portInfo => AddProviderRouterPort(networkPlan, portInfo))
            .SequenceSerial()
        select JoinPlans(plans, networkPlan);

    private static EitherAsync<Error, NetworkPlan> AddProviderRouterPort(
        NetworkPlan networkPlan,
        ProviderRouterPortInfo portInfo) =>
        from _ in RightAsync<Error,Unit>(unit)
        let providerName = portInfo.Subnet.Subnet.ProviderName
        let subnetName = portInfo.Subnet.Subnet.Name
        from externalIpAssignment in portInfo.Port.IpAssignments
            .ToSeq().HeadOrNone()
            .ToEitherAsync(Error.New($"The port for provider '{providerName}' has no IP Address assigned."))
        from parsedExternalIp in Try(() => IPAddress.Parse(externalIpAssignment.IpAddress))
            .ToEither(_ => Error.New($"The port for provider '{providerName}' has an invalid IP Address assigned."))
            .ToAsync()
        from externalNetwork in Try(() => IPNetwork2.Parse(portInfo.Subnet.Subnet.IpNetwork))
            .ToEither(_ => Error.New($"The subnet '{subnetName}' of the provider '{providerName}' has an invalid IP Network assigned."))
            .ToAsync()
        from gatewayIp in Try(() => IPAddress.Parse(portInfo.Subnet.Config.Gateway))
            .ToEither(_ => Error.New($"The subnet '{subnetName}' of the provider '{providerName}' has an invalid gateway IP Address assigned."))
            .ToAsync()
        let updatedPlan = networkPlan
            .AddRouterPort($"externalNet-{networkPlan.Id}-{portInfo.Network.NetworkProvider}",
                $"project-{networkPlan.Id}-{portInfo.Network.Name}",
                portInfo.Port.MacAddress, parsedExternalIp, externalNetwork, "local")

            .AddNATRule($"project-{networkPlan.Id}-{portInfo.Network.Name}", "snat",
                parsedExternalIp, "", portInfo.Network.IpNetwork)

            .AddStaticRoute($"project-{networkPlan.Id}-{portInfo.Network.Name}", "0.0.0.0/0", gatewayIp)
        select updatedPlan;

    private static EitherAsync<Error, NetworkPlan> AddExternalNetSwitches(
        NetworkPlan networkPlan,
        Seq<ProviderSubnetInfo> subnets,
        Seq<NetworkProvider> overlayProviders) =>
        from plans in subnets.Map(x => x.Subnet.ProviderName).Distinct()
            .Map(providerName => AddExternalNetSwitch(networkPlan, providerName, overlayProviders))
            .SequenceSerial()
        select JoinPlans(plans, networkPlan);

    private static EitherAsync<Error, NetworkPlan> AddExternalNetSwitch(
        NetworkPlan networkPlan,
        string providerName,
        Seq<NetworkProvider> overlayProvider) =>
        from provider in overlayProvider.Find(x => x.Name == providerName)
            .ToEitherAsync(Error.New($"Network provider {providerName} not found."))
        let switchName = $"externalNet-{networkPlan.Id}-{provider.Name}"
        let p1 = networkPlan.AddSwitch(switchName)
        let p2 = AddExternalNetworkPortUnique(
            p1, switchName, provider.Name, provider.BridgeName, provider.Vlan)
        select p2;

    private static NetworkPlan AddExternalNetworkPortUnique(
        NetworkPlan plan,
        string switchName,
        string externalNetwork,
        string bridgeName,
        int? tag)
    {
        // Include the bridge name in the port name. This ensures that the patch port
        // of the OVN network is always connected to the correct bridge.
        var name = $"SN-{switchName}-{externalNetwork}-{bridgeName}";

        return plan with
        {
            PlannedSwitchPorts =
            plan.PlannedSwitchPorts.Add(name, new PlannedSwitchPort(switchName)
            {
                Name = name,
                Type = "localnet",
                Options = Map(("network_name", externalNetwork)),
                Addresses = Seq1("unknown"),
                ExternalIds = Map(("network_plan", plan.Id)),
                Tag = tag
            })
        };
    }

    private static NetworkPlan AddNetworksAsSwitches(NetworkPlan networkPlan, Seq<VirtualNetwork> networks, 
        string[] dnsNames) =>
        networks.Map(network => networkPlan.AddSwitch(network.Id.ToString(), dnsNames))
            .Apply(s => JoinPlans(s, networkPlan));


    private static NetworkPlan AddSubnetsAsDhcpOptions(NetworkPlan networkPlan, Seq<VirtualNetwork> networks) =>
        (from network in networks.Where(x=>x.RouterPort!=null && x.RouterPort.IpAssignments.Count > 0)
            let networkIp = network.RouterPort.IpAssignments.First()
         from subnet in network.Subnets
            let p1 = networkPlan.AddDHCPOptions(
                subnet.Id.ToString(), IPNetwork2.Parse(subnet.IpNetwork),
                Map(
                    ("server_id", networkIp.IpAddress ),
                    ("server_mac", network.RouterPort.MacAddress ),
                    ("lease_time", subnet.DhcpLeaseTime == 0 ? "3600" : subnet.DhcpLeaseTime.ToString() ),
                    ("mtu", subnet.MTU == 0 ? "1400" : subnet.MTU.ToString() ),
                    ("dns_server", string.IsNullOrWhiteSpace(subnet.DnsServersV4) ? "9.9.9.9" :
                        $"{{{string.Join(',', subnet.DnsServersV4.Split(','))}}}"),
                    ("router", networkIp.IpAddress ),
                    ("domain_name", $"\\\\\\\"{subnet.DnsDomain}\\\\\\\"")
                )
            )
            select p1)
        .Apply(s => JoinPlans(s.ToSeq(), networkPlan));


    private static NetworkPlan AddCatletPorts(NetworkPlan networkPlan, Seq<CatletNetworkPort> ports) =>
        ports.Map(port => networkPlan.AddNetworkPort(
                port.Network.Id.ToString(), port.OvsName, port.MacAddress,
                port.IpAssignments.HeadOrNone().Map(h => IPAddress.Parse(h.IpAddress))
                    .IfNone(IPAddress.None), 
                        port.IpAssignments?.FirstOrDefault()?.SubnetId?.ToString() ?? ""))
            .Apply(s => JoinPlans(s, networkPlan));

    private static Seq<CatletDnsInfo> MapCatletPortsToDnsInfos(Seq<CatletNetworkPort> ports) =>
        ports.Where(x => !string.IsNullOrWhiteSpace(x.AddressName))
            .Map(port => new CatletDnsInfo(
                port.CatletMetadataId.ToString(),
                port.IpAssignments.SelectMany(a => Prelude.Seq(
                        (Name: $"{port.AddressName}.{a.Subnet.DnsDomain}", Value: a.IpAddress),
                        (Name: GetPTRFromAddress(a.IpAddress), Value: $"{port.AddressName}.{a.Subnet.DnsDomain}")))
                    .GroupBy(x => x.Name)
                    .Map(g => (g.Key, string.Join(' ', g.Map(gi => gi.Value))))
                    .ToMap()));
   
    private static string GetPTRFromAddress(string address) =>
        string.Join(".", address.Split('.').Reverse().ToArray()) + ".in-addr.arpa";

    private static NetworkPlan AddDnsNames(NetworkPlan networkPlan,
        Seq<CatletDnsInfo> catletDnsInfos ) =>
        catletDnsInfos.Map(info => networkPlan.AddDnsRecords(
                info.CatletMetadataId,
                info.Addresses,
                // Mark the DNS records as owned by OVN. OVN will then not forward
                // DNS requests (A, AAAA, ANY) for these records when it cannot resolve
                // them itself. This prevents DNS requests from being forwarded when
                // only IPv4 or IPv6 is configured.
                Map(("ovn-owned", "true"))))
            .Apply(s => JoinPlans(s, networkPlan));


    private static NetworkPlan AddFloatingPorts(NetworkPlan networkPlan, Seq<FloatingPortInfo> ports)
    {
        return ports
            .Where(x=>x.Port.IpAssignments?.Count > 0)
            .Where(x => x.Port.AssignedPort?.IpAssignments?.Count > 0)
            .Map(portInfo =>
            {
                var externalIpAddress = portInfo.Port.IpAssignments.First().IpAddress.Apply(IPAddress.Parse);
                var internalIp = portInfo.Port.AssignedPort.IpAssignments.First().IpAddress;

                return networkPlan.AddNATRule(
                    $"project-{networkPlan.Id}-{portInfo.Port.AssignedPort.Network.Name}",
                    "dnat_and_snat",
                    externalIpAddress, portInfo.Port.MacAddress,
                    internalIp, portInfo.Port.AssignedPort.OvsName);

            })
            .Apply(s => JoinPlans(s, networkPlan));
    }

    private static NetworkPlan AddProjectRouterAndPorts(NetworkPlan networkPlan, Seq<VirtualNetwork> networks)
    {
        return networks.Map(network =>
        {
            var ipNetwork = IPNetwork2.Parse(network.IpNetwork);
            if (network.RouterPort == null || network.RouterPort.IpAssignments?.Count == 0)
                return networkPlan;
            
            networkPlan = networkPlan.AddRouter($"project-{networkPlan.Id}-{network.Name}");

            return networkPlan.AddRouterPort(network.Id.ToString(),
                $"project-{networkPlan.Id}-{network.Name}", network.RouterPort.MacAddress, 
                network.RouterPort.IpAssignments!.First().IpAddress.Apply(IPAddress.Parse), ipNetwork);
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