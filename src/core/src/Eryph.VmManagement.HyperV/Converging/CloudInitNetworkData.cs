using System.Collections.Generic;
using System.Linq;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources.Machines;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging;

/// <summary>
/// Builds the cloud-init network-config (version 1) entries for a catlet adapter.
/// Flat networks that have a provider subnet are configured with static IPs as there is
/// no eryph-managed DHCP server on a flat network. All other networks use DHCP.
/// </summary>
internal static class CloudInitNetworkData
{
    public static object CreateAdapterConfig(
        string adapterName,
        string macAddress,
        Seq<CatletNetworkConfig> networks,
        Seq<MachineNetworkSettings> networkSettings)
    {
        var adapterSettings = networkSettings.Filter(s => s.AdapterName == adapterName);
        var subnets = networks
            .Filter(n => n.AdapterName == adapterName)
            .Bind(n => CreateSubnets(adapterSettings.Find(s => s.NetworkName == n.Name)))
            .ToArray();
        var mtu = adapterSettings.Bind(s => Optional(s.Mtu)).HeadOrNone();

        return CreatePhysicalConfig(adapterName, macAddress, mtu, subnets);
    }

    private static Seq<object> CreateSubnets(
        Option<MachineNetworkSettings> settings) =>
        settings.Filter(IsStaticV4).Match(
            Some: s => s.AddressesV4.ToSeq().Map(address => CreateStaticSubnet(s, address)),
            None: () => Seq1((object)new Dictionary<string, object> { ["type"] = "dhcp" }));

    private static bool IsStaticV4(MachineNetworkSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.GatewayV4)
        && settings.PrefixLengthV4.HasValue
        && settings.AddressesV4 is { Count: > 0 };

    private static object CreateStaticSubnet(
        MachineNetworkSettings settings,
        string address)
    {
        var subnet = new Dictionary<string, object>
        {
            ["type"] = "static",
            ["address"] = $"{address}/{settings.PrefixLengthV4!.Value}",
            ["gateway"] = settings.GatewayV4!,
        };

        if (settings.DnsServersV4 is { Count: > 0 })
            subnet["dns_nameservers"] = settings.DnsServersV4.ToArray();
        if (!string.IsNullOrWhiteSpace(settings.DnsDomain))
            subnet["dns_search"] = new[] { settings.DnsDomain };

        return subnet;
    }

    private static object CreatePhysicalConfig(
        string name,
        string macAddress,
        Option<int> mtu,
        object[] subnets)
    {
        var config = new Dictionary<string, object>
        {
            ["type"] = "physical",
            ["name"] = name,
            ["mac_address"] = macAddress,
            ["subnets"] = subnets,
        };

        mtu.IfSome(value => config["mtu"] = value);

        return config;
    }
}
