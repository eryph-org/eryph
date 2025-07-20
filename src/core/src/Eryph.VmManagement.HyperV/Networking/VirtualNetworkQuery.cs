using Dbosoft.OVN.Windows;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;

using static Eryph.Core.NetworkPrelude;
using static Eryph.VmManagement.Wmi.WmiUtils;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Networking;

public static class VirtualNetworkQuery
{
    public static EitherAsync<Error, Seq<MachineNetworkData>> GetNetworksByAdapters(
        VMHostMachineData hostInfo,
        Seq<TypedPsObject<VirtualMachineDeviceInfo>> networkAdapters) =>
        GetNetworksByAdapters(
            hostInfo,
            networkAdapters.Map(x => x.Cast<VMNetworkAdapter>().Value));

    public static EitherAsync<Error, Seq<MachineNetworkData>> GetNetworksByAdapters(
        VMHostMachineData hostInfo,
        Seq<VMNetworkAdapter> networkAdapters) =>
        use(() => new HyperVOvsPortManager(),
                portManager => GetNetworksByAdapters(networkAdapters, portManager)
                    .ToEither())
            .ToAsync();

    private static EitherAsync<Error, Seq<MachineNetworkData>> GetNetworksByAdapters(
        Seq<VMNetworkAdapter> networkAdapters,
        IHyperVOvsPortManager portManager) =>
        networkAdapters.Map(adapter => GetNetworkByAdapter(adapter, portManager))
            .SequenceSerial();

    private static EitherAsync<Error, MachineNetworkData> GetNetworkByAdapter(
        VMNetworkAdapter networkAdapter,
        IHyperVOvsPortManager portManager) =>
        from portName in portManager.GetConfiguredPortName(networkAdapter.Id)
        from networkData in VirtualNetworkQuery<WmiRuntime>
            .getNetworkByAdapter(networkAdapter, portName)
            .MapFail(e => Error.New($"Failed to query network information for adapter '{networkAdapter.Id}'.", e))
            .Run(WmiRuntime.New()).ToEither().ToAsync()
        select networkData;
}

public static class VirtualNetworkQuery<RT> where RT : struct, HasWmi<RT>
{
    public static Eff<RT, MachineNetworkData> getNetworkByAdapter(
        VMNetworkAdapter adapter,
        Option<string> portName) =>
        from _ in SuccessEff<RT, Unit>(unit)
        let guestNetworkId = adapter.Id.Replace("Microsoft:", @"Microsoft:GuestNetwork\")
        from wmiObjects in Wmi<RT>.executeQuery(
            @"root\virtualization\v2",
            Seq("DefaultGateways", "DHCPEnabled", "DNSServers", "IPAddresses", "Subnets"),
            "Msvm_GuestNetworkAdapterConfiguration",
            $"InstanceID = '{guestNetworkId.Replace(@"\", @"\\")}'")
        from guestNetworkData in wmiObjects.HeadOrNone()
            .ToEff(Error.New("No network information has been returned."))
        from defaultGateways in getRequiredValue<string[]>(guestNetworkData, "DefaultGateways")
        from dnsServers in getRequiredValue<string[]>(guestNetworkData, "DNSServers")
        from dhcpEnabled in getRequiredValue<bool>(guestNetworkData, "DHCPEnabled")
        from ipAddresses in getRequiredValue<string[]>(guestNetworkData, "IPAddresses")
        from netmasks in getRequiredValue<string[]>(guestNetworkData, "Subnets")
        from subnets in convertSubnets(ipAddresses.ToSeq(), netmasks.ToSeq())
        select new MachineNetworkData
        {
            PortName = portName.IfNoneUnsafe((string)null),
            AdapterName = adapter.Name,
            MacAddress = Optional(adapter.MacAddress)
                // Hyper-V returns all zeros when a dynamic MAC address has not been assigned yet.
                .Filter(a => a != "000000000000")
                .IfNoneUnsafe((string)null),
            IPAddresses = ipAddresses,
            DefaultGateways = defaultGateways,
            DnsServers = dnsServers,
            DhcpEnabled = dhcpEnabled,
            Subnets = subnets.ToArray()
        };

    private static Eff<Seq<string>> convertSubnets(
        Seq<string> ipAddresses,
        Seq<string> netmasks) =>
        from _ in guard(ipAddresses.Count == netmasks.Count,
                Error.New("The number of IP addresses and netmasks do not match."))
            .ToEff()
        from subnets in ipAddresses.Zip(netmasks)
            .Map(t => convertSubnet(t.Left, t.Right))
            .ToSeq()
            .Sequence()
        select subnets;

    /// <summary>
    /// Converts the information provided by Hyper-V into a subnet
    /// in CIDR notation as we expect it (e.g. <c>10.0.0.0/20</c>).
    /// </summary>
    /// <remarks>
    /// Hyper-V encodes the subnet in two different ways:
    /// <para>
    /// IPv4: <c>255.255.240.0</c>
    /// </para>
    /// <para>
    /// IPv6: <c>/64</c>
    /// </para>
    /// </remarks>
    private static Eff<string> convertSubnet(
        string ipAddress,
        string netmask) =>
        from subnet in netmask switch
        {
            _ when netmask.StartsWith("/") => parseIPNetwork2(ipAddress + netmask)
                .ToEff(Error.New($"The subnet '{ipAddress + netmask}' is invalid.")),
            _ when !netmask.Contains('.') => parseIPNetwork2($"{ipAddress}/{netmask}")
                .ToEff(Error.New($"The subnet '{ipAddress}/{netmask}' is invalid.")),
            _ => parseIPNetwork2(ipAddress, netmask)
                .ToEff(Error.New($"IP Address '{ipAddress}' with netmask '{netmask}' is not a valid subnet.")),
        }
        select subnet.ToString();
}
