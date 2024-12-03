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
using LanguageExt;
using LanguageExt.Common;

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
                portManager => GetNetworksByAdapters(hostInfo, networkAdapters, portManager)
                    .ToEither())
            .ToAsync();

    private static EitherAsync<Error, Seq<MachineNetworkData>> GetNetworksByAdapters(
        VMHostMachineData hostInfo,
        Seq<VMNetworkAdapter> networkAdapters,
        IHyperVOvsPortManager portManager) =>
        networkAdapters.Map(adapter => GetNetworkByAdapter(hostInfo, adapter, portManager))
            .SequenceSerial(); 

    private static EitherAsync<Error, MachineNetworkData> GetNetworkByAdapter(
        VMHostMachineData hostInfo,
        VMNetworkAdapter networkAdapter,
        IHyperVOvsPortManager portManager) =>
        from portName in portManager.GetPortName(networkAdapter.Id)
        from networkData in Try(() =>
        {
            // TODO Properly implement this query including disposal
            var scope = new ManagementScope(@"\\.\root\virtualization\v2");
            var guestNetworkId = networkAdapter.Id.Replace("Microsoft:", "Microsoft:GuestNetwork\\")
                .Replace("\\", "\\\\");

            var portId = networkAdapter.Id.Replace("\\", "\\\\");

            using var guestAdapterObj = new ManagementObject();
            guestAdapterObj.Path = new ManagementPath(scope.Path +
                                                      $":Msvm_GuestNetworkAdapterConfiguration.InstanceID=\"{guestNetworkId}\"");
            guestAdapterObj.Get();

            using var portSettingsObj = new ManagementObject();
            portSettingsObj.Path = new ManagementPath(scope.Path +
                                                      $":Msvm_EthernetPortAllocationSettingData.InstanceID=\"{portId}\"");

            var info = new MachineNetworkData
            {
                PortName = portName,
                AdapterName = networkAdapter.Name,
                MacAddress = Optional(networkAdapter.MacAddress)
                    // Hyper-V returns all zeros when a dynamic MAC address has not been assigned yet.s
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
