using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using Dbosoft.OVN.Windows;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Common;

using static Eryph.Core.Prelude;
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
        from foo in VirtualNetworkQuery<WmiRuntime>
            .GetNetworkByAdapter(networkAdapter, portName)
            .Run(WmiRuntime.New()).ToEither().ToAsync()

        from networkData in Try(() =>
        {
            // TODO Properly implement this query including disposal
            var scope = new ManagementScope(@"\\.\root\virtualization\v2");
            var guestNetworkId = networkAdapter.Id.Replace("Microsoft:", "Microsoft:GuestNetwork\\")
                .Replace("\\", "\\\\");

            using var guestAdapterObj = new ManagementObject();
            guestAdapterObj.Path = new ManagementPath(scope.Path +
                                                      $":Msvm_GuestNetworkAdapterConfiguration.InstanceID=\"{guestNetworkId}\"");
            guestAdapterObj.Get();

            var info = new MachineNetworkData
            {
                PortName = portName.IfNoneUnsafe((string)null),
                AdapterName = networkAdapter.Name,
                MacAddress = Optional(networkAdapter.MacAddress)
                    // Hyper-V returns all zeros when a dynamic MAC address has not been assigned yet.
                    .Filter(a => a != "000000000000")
                    .IfNoneUnsafe((string)null),
                IPAddresses = ObjectToStringArray(guestAdapterObj.GetPropertyValue("IPAddresses")),
                DefaultGateways = ObjectToStringArray(guestAdapterObj.GetPropertyValue("DefaultGateways")),
                DnsServers = ObjectToStringArray(guestAdapterObj.GetPropertyValue("DNSServers")),
                DhcpEnabled = (bool)guestAdapterObj.GetPropertyValue("DHCPEnabled")
            };
            info.Subnets = AddressesAndSubnetsToSubnets(info.IPAddresses,
                ObjectToStringArray(guestAdapterObj.GetPropertyValue("Subnets"))).ToArray();

            return info;
        }).ToEither(ex => Error.New($"Failed to query network information for adapter '{networkAdapter.Id}'.", Error.New(ex)))
            .ToAsync()
        select networkData;

    private static IEnumerable<string> AddressesAndSubnetsToSubnets(
        IReadOnlyList<string> ipAddresses,
        IReadOnlyList<string> netmasks)
    {
        for (var i = 0; i < ipAddresses.Count; i++)
        {
            var address = ipAddresses[i];
            var netmask = netmasks[i];
            if (netmask.StartsWith("/"))
            {
                yield return IPNetwork2.Parse(address + netmask).ToString();
            }
            else
            {
                if (netmask.IndexOf('.') == -1)
                    yield return IPNetwork2.Parse($"{address}/{netmask}").ToString();
                else
                {
                    yield return IPNetwork2.Parse(address, netmask).ToString();
                }
            }

        }
    }

    private static string[] ObjectToStringArray(object value)
    {
        if (value != null && value is IEnumerable enumerable) return enumerable.Cast<string>().ToArray();

        return [];
    }
}

public static class VirtualNetworkQuery<RT> where RT : struct, HasWmi<RT>
{
    public static Eff<RT, MachineNetworkData> GetNetworkByAdapter(
        VMNetworkAdapter adapter,
        Option<string> portName) =>
        from _ in SuccessEff<RT, Unit>(unit)
        let guestNetworkId = adapter.Id.Replace("Microsoft:", @"Microsoft:GuestNetwork\")
        from wmiObjects in Wmi<RT>.executeQuery(
            @"root\virtualization\v2",
            Seq("DefaultGateways", "DHCPEnabled", "DNSServers", "IPAddresses", "Subnets"),
            "Msvm_GuestNetworkAdapterConfiguration",
            $"InstanceID = '{guestNetworkId.Replace(@"\", @"\\")}%'")
        from guestNetworkData in wmiObjects.HeadOrNone()
            .ToEff(Error.New("No network information has been returned."))
        from defaultGateways in getRequiredValue<string[]>(guestNetworkData, "DefaultGateways")
        from dnsServers in getRequiredValue<string[]>(guestNetworkData, "DNSServers")
        from dhcpEnabled in getRequiredValue<bool>(guestNetworkData, "DHCPEnabled")
        from ipAddresses in getRequiredValue<string[]>(guestNetworkData, "IPAddresses")
        from netmasks in getRequiredValue<string[]>(guestNetworkData, "Subnets")
        select new MachineNetworkData();

    // TODO use zip
    /*
    private static Eff<Seq<string>> convertSubnets(
        Seq<string> ipAddresses,
        Seq<string> netmasks) =>
        from subnets in ipAddresses
            .Zip((LanguageExt.Seq<>.Empty))
            .Map((i, ipAddress) => convertSubnet(ipAddress, netmasks.Skip(i).HeadOrNone()))
            .ToSeq()
            .Sequence()
        select subnets;

    private static Eff<string> convertSubnet(
        string ipAddress,
        Option<string> netmask) =>
        from validNetmask in netmask
            .ToEff(Error.New($"Netmask for IP address {ipAddress} is missing."))
        from subnet in validNetmask switch
        {
            _ when validNetmask.StartsWith("/") => parseIPNetwork2(ipAddress + validNetmask)
                .ToEff(Error.New($"The subnet '{ipAddress + validNetmask}' is invalid.")),
            _ when validNetmask.IndexOf('.') == -1 => parseIPNetwork2($"{ipAddress}/{validNetmask}")
                .ToEff(Error.New($"The subnet '{ipAddress}/{validNetmask}' is invalid.")),
            _ => parseIPNetwork2(ipAddress, validNetmask)
                .ToEff(Error.New($"IP Address '{ipAddress}' with netmask '{validNetmask}' is not a valid subnet.s")),
        }
        select subnet.ToString();
    */
}
