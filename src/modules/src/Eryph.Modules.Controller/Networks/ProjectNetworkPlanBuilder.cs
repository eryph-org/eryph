using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Dbosoft.OVN;
using Dbosoft.OVN.Model.OVN;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.Common;

using static Eryph.Core.NetworkPrelude;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Networks;

internal class ProjectNetworkPlanBuilder(
    IStateStore stateStore)
    : IProjectNetworkPlanBuilder
{
    private sealed record FloatingPortInfo(FloatingNetworkPort Port);

    private sealed record ProviderRouterPortInfo(
        ProviderRouterPort Port,
        VirtualNetwork Network,
        ProviderSubnetInfo Subnet);

    private sealed record ProviderSubnetInfo(ProviderSubnet Subnet, NetworkProviderSubnet Config);

    private sealed record CatletDnsInfo(string CatletMetadataId, Map<string, string> Addresses);

    private sealed record ProviderRouterIpInfo(
        string ProviderName,
        IPAddress ProjectRouterIp,
        IPAddress ProviderRouterIp,
        IPNetwork2 Network);

    public EitherAsync<Error, NetworkPlan> GenerateNetworkPlan(
        Guid projectId,
        NetworkProvidersConfiguration providerConfig) =>
        from _ in RightAsync<Error, Unit>(unit)
        let networkPlan = new NetworkPlan(projectId.ToString())
        let overLayProviders = providerConfig.NetworkProviders.ToSeq()
            .Filter(x => x.Type is NetworkProviderType.NatOverlay or NetworkProviderType.Overlay)
        
        from networks in GetAllOverlayNetworks(projectId, overLayProviders)

        from providerSubnets in GetProviderSubnetInfos(networks, overLayProviders)
        from providerRouterPorts in GetProviderRouterPortInfos(networks, providerSubnets)
        from providerRouterIpInfos in PrepareProviderRouterIpInfos(providerConfig)

        let catletPorts = networks.ToSeq()
            .Bind(n => n.NetworkPorts.OfType<CatletNetworkPort>().ToSeq())
        from floatingPorts in GetFloatingPortInfos(catletPorts, providerSubnets)
        let dnsInfo = MapCatletPortsToDnsInfos(catletPorts)
        let dnsNames = dnsInfo.Map(info => info.CatletMetadataId)

        from p1 in AddProjectRouterAndPorts(networkPlan, networks, providerRouterIpInfos)
        from p2 in AddExternalNetSwitches(p1, providerSubnets, overLayProviders)
        from p3 in AddProviderRouterPorts(p2, providerRouterPorts, providerRouterIpInfos)
        let p4 = AddNetworksAsSwitches(p3, networks, dnsNames)
        from p5 in AddSubnetsAsDhcpOptions(p4, networks, providerRouterPorts)
        let p6 = AddCatletPorts(p5, catletPorts)
        let p7 = AddFloatingPorts(p6, floatingPorts)
        let p8 = AddDnsNames(p7, dnsInfo)
        select p8;

    /// <summary>
    /// Prepares IP addresses for the peer ports between the project router and the provider routers.
    /// </summary>
    /// <remarks>
    /// These IP addresses are an implementation detail as they are used only to route traffic
    /// internally. We just calculate them based on the list of the configured network providers.
    /// </remarks>
    private static EitherAsync<Error, HashMap<string, ProviderRouterIpInfo>> PrepareProviderRouterIpInfos(
        NetworkProvidersConfiguration providersConfig) =>
        from _1 in guard(providersConfig.NetworkProviders.Length <= EryphConstants.Limits.MaxNetworkProviders,
                Error.New("Too many network providers are configured."))
            .ToEitherAsync()
        from optionalEastWestNetwork in Optional(providersConfig.EastWestNetwork)
            .Map(n => parseIPNetwork2(n).ToEitherAsync(Error.New($"The east-west network '{n}' is invalid.")))
            .Sequence()
        let eastWestNetwork = optionalEastWestNetwork.IfNone(IPNetwork2.Parse(EryphConstants.DefaultEastWestNetwork))
        from _2 in guard(eastWestNetwork.Cidr <= 24,
            Error.New($"The east-west network '{eastWestNetwork}' is too small. The network must be /24 or larger."))
        let baseIp = IPNetwork2.ToBigInteger(eastWestNetwork.Network)
        let infos = providersConfig.NetworkProviders.ToSeq()
            .Map((i, p) => new ProviderRouterIpInfo(
                p.Name,
                IPNetwork2.ToIPAddress(baseIp + 2 * i, AddressFamily.InterNetwork),
                IPNetwork2.ToIPAddress(baseIp + 2 * i + 1, AddressFamily.InterNetwork),
                // These IP addresses are used for point-to-point links between two routers
                // which use peer ports. Hence, we can use the /31 networks which contain only
                // two IP addresses.
                new IPNetwork2(IPNetwork2.ToIPAddress(baseIp + 2 * i, AddressFamily.InterNetwork), 31)))
            .Map(i => (providerName: i.ProviderName, i))
            .ToSeq()
        select infos.ToHashMap();

    private EitherAsync<Error, Seq<ProviderRouterPortInfo>> GetProviderRouterPortInfos(
        Seq<VirtualNetwork> networks,
        Seq<ProviderSubnetInfo> providerSubnets) =>
        from _ in RightAsync<Error, Unit>(unit)
        let providerPorts = networks.Bind(n => n.NetworkPorts.OfType<ProviderRouterPort>().ToSeq())
        from portInfos in providerPorts
            .Map(portInfo => GetProviderRouterPortInfo(portInfo, providerSubnets))
            .SequenceSerial()
        select portInfos;

    private EitherAsync<Error, ProviderRouterPortInfo> GetProviderRouterPortInfo(
        ProviderRouterPort providerPort,
        Seq<ProviderSubnetInfo> providerSubnets) =>
        from providerSubnet in providerSubnets
            .Find(s => s.Subnet.ProviderName == providerPort.Network.NetworkProvider &&
                       s.Subnet.Name == providerPort.SubnetName)
            .ToEitherAsync(Error.New(
                $"Network '{providerPort.Network.Name}' configuration error: subnet {providerPort.SubnetName} of network provider {providerPort.Network.NetworkProvider} not found."))
        select new ProviderRouterPortInfo(providerPort, providerPort.Network, providerSubnet);

    private static EitherAsync<Error, Seq<FloatingPortInfo>> GetFloatingPortInfos(
        Seq<CatletNetworkPort> catletPorts,
        Seq<ProviderSubnetInfo> providerSubnets) =>
        catletPorts.Filter(p => p.FloatingPort != null && p.IpAssignments.Count > 0)
            .Map(p => GetFloatingPortInfo(p, providerSubnets))
            .SequenceSerial();

    private static EitherAsync<Error, FloatingPortInfo> GetFloatingPortInfo(
        CatletNetworkPort catletPort,
        Seq<ProviderSubnetInfo> providerSubnets) =>
        from floatingPort in Optional(catletPort.FloatingPort)
            .ToEitherAsync(Error.New($"BUG! The catlet port '{catletPort.Name}' has no floating port."))
        from _ in providerSubnets
            .Find(s => s.Subnet.ProviderName == floatingPort.ProviderName && s.Subnet.Name == floatingPort.SubnetName)
            .ToEitherAsync(Error.New(
                $"Port '{floatingPort.Name}' configuration error: subnet {floatingPort.SubnetName} of network provider {floatingPort.ProviderName} not found."))
        select new FloatingPortInfo(floatingPort);

    private EitherAsync<Error, Seq<ProviderSubnetInfo>> GetProviderSubnetInfos(
        Seq<VirtualNetwork> networks,
        Seq<NetworkProvider> overlayProviderConfigs) =>
        from providerSubnets in stateStore.For<ProviderSubnet>().IO
            .ListAsync(new NetplanBuilderSpecs.GetAllProviderSubnets())
        let providerPorts = networks.Bind(n => n.NetworkPorts.OfType<ProviderRouterPort>().ToSeq())
        from providerSubnetInfos in providerPorts
            .Map(p => GetProviderSubnetInfo(p, overlayProviderConfigs, providerSubnets))
            .SequenceSerial()
        select providerSubnetInfos;

    private EitherAsync<Error, ProviderSubnetInfo> GetProviderSubnetInfo(
        ProviderRouterPort providerPort,
        Seq<NetworkProvider> overlayProviderConfigs,
        Seq<ProviderSubnet> providerSubnets) =>
        from _ in RightAsync<Error, Unit>(unit)
        let networkName = providerPort.Network.Name
        let providerName = providerPort.Network.NetworkProvider
        from providerConfig in overlayProviderConfigs.Find(pc => pc.Name == providerName)
            .ToEitherAsync(Error.New(
                $"Network '{networkName}' configuration error: network provider '{providerName}' not found."))
        from providerSubnetConfig in providerConfig.Subnets.ToSeq()
            .Find(s => s.Name == providerPort.SubnetName)
            .ToEitherAsync(Error.New(
                $"Network '{networkName}' configuration error: subnet '{providerPort.SubnetName}' not found in network provider '{providerName}'."))
        from providerSubnet in providerSubnets
            .Find(s => s.ProviderName == providerName && s.Name == providerPort.SubnetName)
            .ToEitherAsync(Error.New(
                $"BUG! Subnet '{providerPort.SubnetName}' of network provider '{providerName}' not found in state DB."))
        select new ProviderSubnetInfo(providerSubnet, providerSubnetConfig);

    private EitherAsync<Error, Seq<VirtualNetwork>> GetAllOverlayNetworks(
        Guid projectId,
        Seq<NetworkProvider> overLayProviders) =>
        stateStore.For<VirtualNetwork>().IO
            .ListAsync(new NetplanBuilderSpecs.GetAllNetworks(projectId))
            .Map(s => s.Filter(x => overLayProviders.Any(p => p.Name == x.NetworkProvider)));

    private static EitherAsync<Error, NetworkPlan> AddProviderRouterPorts(
        NetworkPlan networkPlan,
        Seq<ProviderRouterPortInfo> ports,
        HashMap<string, ProviderRouterIpInfo> providerRouterIpInfos) =>
        from plans in ports
            .Map(portInfo => AddProviderRouterPort(networkPlan, portInfo, providerRouterIpInfos))
            .SequenceSerial()
        select JoinPlans(plans, networkPlan);

    private static EitherAsync<Error, NetworkPlan> AddProviderRouterPort(
        NetworkPlan networkPlan,
        ProviderRouterPortInfo portInfo,
        HashMap<string, ProviderRouterIpInfo> providerRouterIpInfos) =>
        from _ in RightAsync<Error, Unit>(unit)
        let providerName = portInfo.Subnet.Subnet.ProviderName
        from providerRouterIpInfo in providerRouterIpInfos
            .Find(providerName)
            .ToEitherAsync(Error.New($"BUG! Could not find the IP information for provider '{providerName}'."))
        let subnetName = portInfo.Subnet.Subnet.Name
        from externalIpAssignment in portInfo.Port.IpAssignments
            .ToSeq().HeadOrNone()
            .ToEitherAsync(Error.New($"The port for provider '{providerName}' has no IP Address assigned."))
        from parsedExternalIp in Try(() => IPAddress.Parse(externalIpAssignment.IpAddress))
            .ToEither(_ => Error.New($"The port for provider '{providerName}' has an invalid IP Address assigned."))
            .ToAsync()
        from externalNetwork in Try(() => IPNetwork2.Parse(portInfo.Subnet.Subnet.IpNetwork))
            .ToEither(_ => Error.New(
                $"The subnet '{subnetName}' of the provider '{providerName}' has an invalid IP Network assigned."))
            .ToAsync()
        from gatewayIp in Try(() => IPAddress.Parse(portInfo.Subnet.Config.Gateway))
            .ToEither(_ => Error.New(
                $"The subnet '{subnetName}' of the provider '{providerName}' has an invalid gateway IP Address assigned."))
            .ToAsync()
        let updatedPlan = networkPlan
            .AddRouterPort(
                $"externalNet-{networkPlan.Id}-{portInfo.Network.NetworkProvider}",
                $"project-{networkPlan.Id}-{portInfo.Network.NetworkProvider}",
                portInfo.Port.MacAddress,
                parsedExternalIp,
                externalNetwork,
                chassisGroup: "local")
            .AddNATRule(
                $"project-{networkPlan.Id}-{portInfo.Network.NetworkProvider}",
                "snat",
                parsedExternalIp,
                "",
                portInfo.Network.IpNetwork)
            .AddStaticRoute(
                $"project-{networkPlan.Id}-{portInfo.Network.NetworkProvider}",
                "0.0.0.0/0",
                gatewayIp)
            .AddStaticRoute(
                $"project-{networkPlan.Id}-{portInfo.Network.NetworkProvider}",
                portInfo.Network.IpNetwork,
                providerRouterIpInfo.ProjectRouterIp)
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

    private static NetworkPlan AddNetworksAsSwitches(
        NetworkPlan networkPlan,
        Seq<VirtualNetwork> networks,
        Seq<string> dnsNames) =>
        networks.Map(network => networkPlan.AddSwitch(network.Id.ToString(), dnsNames))
            .Apply(s => JoinPlans(s, networkPlan));

    private static EitherAsync<Error, NetworkPlan> AddSubnetsAsDhcpOptions(
        NetworkPlan networkPlan,
        Seq<VirtualNetwork> networks,
        Seq<ProviderRouterPortInfo> providerPortInfos) =>
        from plans in networks
            .Filter(n => n.RouterPort is { IpAssignments.Count: > 0 })
            .Bind(n => n.Subnets.ToSeq())
            .Map(s => AddSubnetAsDhcpOptions(networkPlan, s, providerPortInfos))
            .SequenceSerial()
        select JoinPlans(plans, networkPlan);

    private static EitherAsync<Error, NetworkPlan> AddSubnetAsDhcpOptions(
        NetworkPlan networkPlan,
        VirtualNetworkSubnet subnet,
        Seq<ProviderRouterPortInfo> providerPortInfos) =>
        from subnetNetwork in parseIPNetwork2(subnet.IpNetwork)
            .ToEitherAsync(Error.New($"The network '{subnet.IpNetwork}' of subnet {subnet.Id} is invalid."))
        from routerPort in Optional(subnet.Network.RouterPort)
            .ToEitherAsync(Error.New($"The network {subnet.NetworkId} has no router port assigned."))
        from networkIp in routerPort.IpAssignments.ToSeq().HeadOrNone()
            .ToEitherAsync(Error.New($"The network {subnet.NetworkId} has no router IP assigned."))
        let providerPortInfo = providerPortInfos.Find(x => x.Network.Id == subnet.NetworkId)
        let leaseTime = subnet.DhcpLeaseTime == 0 ? 3600 : subnet.DhcpLeaseTime
        let mtu = subnet.MTU == 0 ? 1400 : subnet.MTU
        let dnsServers = Optional(subnet.DnsServersV4)
            .Filter(notEmpty)
            .ToSeq()
            .Bind(s => s.Split(',').ToSeq())
            .DefaultIfEmpty("9.9.9.9")
        // Push static routes via DHCP to the catlets. Linux (and likely also other OS
        // using the weak host model) do not necessarily send the response on the same
        // network interface which received the request. We add a specific route for the
        // IP range of the network provider to make sure that natted traffic flows back
        // to the correct provider.
        let routes = providerPortInfo
            .Map(p => $"{p.Subnet.Subnet.IpNetwork},{networkIp.IpAddress}")
            .ToSeq()
            .Append($"0.0.0.0/0,{networkIp.IpAddress}")
        let options = Map(
            ("server_id", networkIp.IpAddress),
            ("server_mac", routerPort.MacAddress),
            ("lease_time", $"{leaseTime}"),
            ("mtu", $"{mtu}"),
            ("dns_server", $"{{{string.Join(',', dnsServers)}}}"),
            ("router", networkIp.IpAddress),
            ("domain_name", $"\\\\\\\"{subnet.DnsDomain}\\\\\\\""),
            ("classless_static_route", $"{{{string.Join(',', routes)}}}"))
        let updatedPlan = networkPlan.AddDHCPOptions(subnet.Id.ToString(), subnetNetwork, options)
        select updatedPlan;

    private static NetworkPlan AddCatletPorts(NetworkPlan networkPlan, Seq<CatletNetworkPort> ports) =>
        ports.Map(port => networkPlan.AddNetworkPort(
                port.Network.Id.ToString(), port.OvsName, port.MacAddress,
                port.IpAssignments.HeadOrNone().Map(h => IPAddress.Parse(h.IpAddress))
                    .IfNone(IPAddress.None),
                port.IpAssignments?.FirstOrDefault()?.SubnetId?.ToString() ?? ""))
            .Apply(s => JoinPlans(s, networkPlan));

    private static Seq<CatletDnsInfo> MapCatletPortsToDnsInfos(
        Seq<CatletNetworkPort> ports) =>
        ports.Where(x => !string.IsNullOrWhiteSpace(x.AddressName))
            .Map(port => new CatletDnsInfo(
                port.CatletMetadataId.ToString(),
                port.IpAssignments.SelectMany(a => Seq(
                        (Name: $"{port.AddressName}.{a.Subnet.DnsDomain}", Value: a.IpAddress),
                        (Name: GetPTRFromAddress(a.IpAddress), Value: $"{port.AddressName}.{a.Subnet.DnsDomain}")))
                    .GroupBy(x => x.Name)
                    .Map(g => (g.Key, string.Join(' ', g.Map(gi => gi.Value))))
                    .ToMap()));

    private static string GetPTRFromAddress(string address) =>
        string.Join(".", address.Split('.').Reverse().ToArray()) + ".in-addr.arpa";

    private static NetworkPlan AddDnsNames(NetworkPlan networkPlan,
        Seq<CatletDnsInfo> catletDnsInfos) =>
        catletDnsInfos.Map(info => networkPlan.AddDnsRecords(
                info.CatletMetadataId,
                info.Addresses,
                // Mark the DNS records as owned by OVN. OVN will then not forward
                // DNS requests (A, AAAA, ANY) for these records when it cannot resolve
                // them itself. This prevents DNS requests from being forwarded when
                // only IPv4 or IPv6 is configured.
                Map(("ovn-owned", "true"))))
            .Apply(s => JoinPlans(s, networkPlan));

    private static NetworkPlan AddFloatingPorts(
        NetworkPlan networkPlan,
        Seq<FloatingPortInfo> ports) =>
        ports
            .Where(x => x.Port.IpAssignments?.Count > 0)
            .Where(x => x.Port.AssignedPort?.IpAssignments?.Count > 0)
            .Map(portInfo =>
            {
                var externalIpAddress = portInfo.Port.IpAssignments.First().IpAddress.Apply(IPAddress.Parse);
                var internalIp = portInfo.Port.AssignedPort.IpAssignments.First().IpAddress;

                return networkPlan.AddNATRule(
                    $"project-{networkPlan.Id}-{portInfo.Port.AssignedPort.Network.NetworkProvider}",
                    "dnat_and_snat",
                    externalIpAddress, portInfo.Port.MacAddress,
                    internalIp,
                    portInfo.Port.AssignedPort.OvsName);

            })
            .Apply(s => JoinPlans(s, networkPlan));

    private static EitherAsync<Error, NetworkPlan> AddProjectRouterAndPorts(
        NetworkPlan networkPlan,
        Seq<VirtualNetwork> networks,
        HashMap<string, ProviderRouterIpInfo> providerRouterIpInfos) =>
        from _ in RightAsync<Error, Unit>(unit)
        let planWithProjectRouter = networkPlan
            .AddRouter($"project-{networkPlan.Id}")
        from providerRouterPlans in networks
            .Map(n => n.NetworkProvider)
            .Distinct()
            .Map(providerName => AddProjectProviderRouterAndPorts(networkPlan, providerName, providerRouterIpInfos))
            .SequenceSerial()
        let planWithProviderRouters = JoinPlans(providerRouterPlans, planWithProjectRouter)
        from routerPortPlans in networks
            .Filter(n => n.RouterPort is { IpAssignments.Count: > 0 })
            .Map(network => AddNetworkRouterPort(planWithProviderRouters, network, providerRouterIpInfos))
            .SequenceSerial()
        let planWithRouterPorts = JoinPlans(routerPortPlans, planWithProviderRouters)
        select planWithRouterPorts;

    private static EitherAsync<Error, NetworkPlan> AddProjectProviderRouterAndPorts(
        NetworkPlan networkPlan,
        string providerName,
        HashMap<string, ProviderRouterIpInfo> providerRouterIpInfos) =>
        from providerRouterIpInfo in providerRouterIpInfos
            .Find(providerName)
            .ToEitherAsync(Error.New($"BUG! Could not find the IP information for provider '{providerName}'."))
        let projectRouterName = $"project-{networkPlan.Id}"
        let projectProviderRouterName = $"project-{networkPlan.Id}-{providerName}"
        // All virtual networks of a project connect to a project router which enables
        // east-west traffic. The project router connects to separate provider routers
        // for each network provider using peer ports.
        let result = networkPlan
            .AddRouter(projectProviderRouterName)
            .AddRouterPeerPort(
                projectRouterName,
                projectProviderRouterName,
                MacAddresses.FormatMacAddress(MacAddresses.GenerateMacAddress($"{projectRouterName}-{projectProviderRouterName}")),
                providerRouterIpInfo.ProjectRouterIp,
                providerRouterIpInfo.Network)
            .AddRouterPeerPort(
                projectProviderRouterName,
                projectRouterName,
                MacAddresses.FormatMacAddress(MacAddresses.GenerateMacAddress($"{projectProviderRouterName}-{projectRouterName}")),
                providerRouterIpInfo.ProviderRouterIp,
                providerRouterIpInfo.Network)
        select result;

    private static EitherAsync<Error, NetworkPlan> AddNetworkRouterPort(
        NetworkPlan networkPlan,
        VirtualNetwork network,
        HashMap<string, ProviderRouterIpInfo> providerRouterIpInfos) =>
        from providerRouterIpInfo in providerRouterIpInfos
            .Find(network.NetworkProvider)
            .ToEitherAsync(Error.New(
                $"BUG! Could not find the IP information for provider '{network.NetworkProvider}'."))
        from ipNetwork in parseIPNetwork2(network.IpNetwork)
            .ToEitherAsync(Error.New(
                $"Network '{network.Name}' configuration error: the IP network '{network.IpNetwork}' is invalid."))
        from _1 in guardnot(network.RouterPort is null,
            Error.New($"BUG! The network '{network.Name}' has no router port."))
        from _2 in guard(network.RouterPort.IpAssignments is { Count: > 0 },
            Error.New($"Network '{network.Name}' configuration error: the router port has no IP assigned."))
        from routerIp in parseIPAddress(network.RouterPort.IpAssignments![0].IpAddress)
            .ToEitherAsync(Error.New(
                $"Network '{network.Name}' configuration error: the router port IP '{network.RouterPort.IpAssignments![0].IpAddress}' is invalid."))
        let result = networkPlan
            .AddRouterPort(
                network.Id.ToString(),
                $"project-{networkPlan.Id}",
                network.RouterPort.MacAddress,
                routerIp,
                ipNetwork,
                routeTable: network.Id.ToString())
            .AddStaticRoute(
                $"project-{networkPlan.Id}",
                "0.0.0.0/0",
                providerRouterIpInfo.ProviderRouterIp,
                routeTable: network.Id.ToString())
        select result;

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