using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Threading.Tasks;
using Haipa.Messages.Events;
using Haipa.Modules.VmHostAgent.Inventory;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using LanguageExt;

namespace Haipa.Modules.VmHostAgent
{
    internal class VirtualMachineInventory
    {

        public Task<Either<PowershellFailure, MachineInfo>> InventorizeVM<T>(TypedPsObject<T> vm) where T: 
            IVirtualMachineCoreInfo, 
            IVMWithStateInfo, 
            IVMWithNetworkAdapterInfo<IVMNetworkAdapterWithConnection>
        {
            var info = new MachineInfo
            {
                MachineId = vm.Value.Id,
                Status = InventoryConverter.MapVmInfoStatusToVmStatus(vm.Value.State),
                Name = vm.Value.Name,
                NetworkAdapters = vm.Value.NetworkAdapters?.Map(a => new VirtualMachineNetworkAdapterInfo
                {
                    AdapterName = a.Name,
                    VirtualSwitchName = a.SwitchName
                }).ToArray(),
                Networks = GetNetworksByAdapters(vm.Value.Id, vm.Value.NetworkAdapters)
            };

            return Prelude.RightAsync<PowershellFailure, MachineInfo>(info).ToEither();
        }

        private static VirtualMachineNetworkInfo[] GetNetworksByAdapters(Guid vmId, IEnumerable<IVMNetworkAdapterWithConnection> networkAdapters)
        {
            var scope = new ManagementScope(@"\\.\root\virtualization\v2");
            var resultList = new List<VirtualMachineNetworkInfo>();

            foreach (var networkAdapter in networkAdapters)
            {
                var guestNetworkId = networkAdapter.Id.Replace("Microsoft:", "Microsoft:GuestNetwork\\").Replace("\\", "\\\\");
                var obj = new ManagementObject();
                var path = new ManagementPath(scope.Path + $":Msvm_GuestNetworkAdapterConfiguration.InstanceID=\"{guestNetworkId}\"");

                obj.Path = path;
                obj.Get();

                var info = new VirtualMachineNetworkInfo
                {
                    AdapterName = networkAdapter.Name,
                    IPAddresses = ObjectToStringArray(obj.GetPropertyValue("IPAddresses")),
                    DefaultGateways = ObjectToStringArray(obj.GetPropertyValue("DefaultGateways")),
                    DnsServers = ObjectToStringArray(obj.GetPropertyValue("DNSServers")),
                    DhcpEnabled = (bool)obj.GetPropertyValue("DHCPEnabled")

                };
                info.Subnets = AddressesAndSubnetsToSubnets(info.IPAddresses,
                    ObjectToStringArray(obj.GetPropertyValue("Subnets"))).ToArray();

                resultList.Add(info);
            }

            return resultList.ToArray();

        }

        private static IEnumerable<string> AddressesAndSubnetsToSubnets(IReadOnlyList<string> ipAddresses, IReadOnlyList<string> netmasks)
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
                    yield return IPNetwork.Parse(address, netmask).ToString();
            }
        }

        private static string[] ObjectToStringArray(object value)
        {
            if (value != null && value is IEnumerable enumerable)
            {
                return enumerable.Cast<string>().ToArray();
            }

            return new string[0];
        }


        //private static string[] GetAddressesByFamily(TypedPsObject<MinimizedVirtualMachineInfo> vm, AddressFamily family)
        //{

        //    return vm.GetList(x => x.NetworkAdapters).Bind(adapter => adapter.Value.IPAddresses.Where(a =>
        //    {
        //        var ipAddress = IPAddress.Parse(a);
        //        return ipAddress.AddressFamily == family;
        //    })).ToArray();
        //}



    }
}