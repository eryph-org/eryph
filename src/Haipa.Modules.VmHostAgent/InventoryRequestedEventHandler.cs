using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Commands;
using Haipa.Messages.Events;
using Haipa.Modules.VmHostAgent.Inventory;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Bus;
using Rebus.Handlers;

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal partial class InventoryRequestedEventHandler : IHandleMessages<InventoryRequestedEvent>
    {

        private readonly IPowershellEngine _engine;
        private readonly IBus _bus;

        public InventoryRequestedEventHandler(IBus bus, IPowershellEngine engine)
        {
            _bus = bus;
            _engine = engine;
        }


        public Task Handle(InventoryRequestedEvent message) => 
            _engine.GetObjectsAsync<MinimizedVirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm"))
                .ToAsync()
                .IfRightAsync(vms => _bus.Send(VmsToInventory(vms)));

        private static UpdateInventoryCommand VmsToInventory(ISeq<TypedPsObject<MinimizedVirtualMachineInfo>> vms)
        {
            
            var inventory = vms.Map(vm =>
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
                    Networks = GetNetworksByAdapters(vm.Value.Id,vm.Value.NetworkAdapters)
                };

                //info.IpV4Addresses = GetAddressesByFamily(vm, AddressFamily.InterNetwork);
                //info.IpV6Addresses = GetAddressesByFamily(vm, AddressFamily.InterNetworkV6);
                return info;
            }).ToList();

            return new UpdateInventoryCommand
            {
                AgentName = Environment.MachineName,
                Inventory = inventory

            };
        }

        private static VirtualMachineNetworkInfo[] GetNetworksByAdapters(Guid vmId, MinimizedVMNetworkAdapter[] networkAdapters)
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
                    DhcpEnabled = (bool) obj.GetPropertyValue("DHCPEnabled") 

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
                    yield return IPNetwork.Parse(address,netmask).ToString();
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