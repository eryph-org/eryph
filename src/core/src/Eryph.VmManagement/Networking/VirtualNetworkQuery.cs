using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;

namespace Eryph.VmManagement.Networking;

public static class VirtualNetworkQuery
{
    public static MachineNetworkData[] GetNetworksByAdapters(VMHostMachineData hostInfo,
        IEnumerable<TypedPsObject<VirtualMachineDeviceInfo>> networkAdapters)
    {
        return GetNetworksByAdapters(hostInfo,
                networkAdapters.Select(x=>x.Cast<VMNetworkAdapter>().Value));
    }

    public static MachineNetworkData[] GetNetworksByAdapters(VMHostMachineData hostInfo,
        IEnumerable<VMNetworkAdapter> networkAdapters)
    {
        var scope = new ManagementScope(@"\\.\root\virtualization\v2");
        var resultList = new List<MachineNetworkData>();

        foreach (var networkAdapter in networkAdapters)
        {
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

            string adapterPortName = null;

            try
            {
                portSettingsObj.Get();
                adapterPortName = portSettingsObj.GetPropertyValue("ElementName") as string;
            }
            catch (ManagementException)
            {
                // expected if not found
            }

            var networkProvider = hostInfo.FindNetworkProvider(networkAdapter.SwitchId, adapterPortName);

            var info = new MachineNetworkData
            {
                NetworkProviderName = networkProvider?.Name,
                PortName = adapterPortName,
                AdapterName = networkAdapter.Name,
                IPAddresses = ObjectToStringArray(guestAdapterObj.GetPropertyValue("IPAddresses")),
                DefaultGateways = ObjectToStringArray(guestAdapterObj.GetPropertyValue("DefaultGateways")),
                DnsServers = ObjectToStringArray(guestAdapterObj.GetPropertyValue("DNSServers")),
                DhcpEnabled = (bool) guestAdapterObj.GetPropertyValue("DHCPEnabled")
            };
            info.Subnets = AddressesAndSubnetsToSubnets(info.IPAddresses,
                ObjectToStringArray(guestAdapterObj.GetPropertyValue("Subnets"))).ToArray();

            resultList.Add(info);
        }

        return resultList.ToArray();
    }

    private static IEnumerable<string> AddressesAndSubnetsToSubnets(IReadOnlyList<string> ipAddresses,
        IReadOnlyList<string> netmasks)
    {
        for (var i = 0; i < ipAddresses.Count; i++)
        {
            var address = ipAddresses[i];
            var netmask = netmasks[i];
            if (netmask.StartsWith("/"))
            {
                yield return IPNetwork.Parse(address + netmask).ToString();
            }
            else
            {
                if (netmask.IndexOf('.') == -1)
                    yield return IPNetwork.Parse($"{address}/{netmask}").ToString();
                else
                {
                    yield return IPNetwork.Parse(address, netmask).ToString();
                }
            }

        }
    }

    private static string[] ObjectToStringArray(object value)
    {
        if (value != null && value is IEnumerable enumerable) return enumerable.Cast<string>().ToArray();

        return Array.Empty<string>();
    }


}