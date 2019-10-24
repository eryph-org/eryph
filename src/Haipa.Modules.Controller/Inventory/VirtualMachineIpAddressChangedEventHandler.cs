using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Haipa.Messages.Events;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Rebus.Handlers;

namespace Haipa.Modules.Controller.Inventory
{
    internal class VirtualMachineIpAddressChangedEventHandler : IHandleMessages<VirtualMachineNetworkChangedEvent>
    {
        private readonly StateStoreContext _stateStoreContext;

        public VirtualMachineIpAddressChangedEventHandler(StateStoreContext stateStoreContext)
        {
            _stateStoreContext = stateStoreContext;
        }

        public async Task Handle(VirtualMachineNetworkChangedEvent message)
        {
            var machine = await _stateStoreContext.Machines
                .Include(x => x.VM)
                .ThenInclude(vm=>vm.NetworkAdapters)
                .Include(x=>x.Networks)
                .FirstOrDefaultAsync(x => x.Id == message.MachineId);

            if (machine?.VM == null)
                return;

            var adapter = machine.VM.NetworkAdapters
                .FirstOrDefault(a => a.Name == message.ChangedAdapter.AdapterName);

            if (adapter == null)
            {
                adapter = new VirtualMachineNetworkAdapter {Name = message.ChangedAdapter.AdapterName, Vm = machine.VM};
                _stateStoreContext.Add(adapter);
            }

            adapter.SwitchName = message.ChangedAdapter.VirtualSwitchName;

            var network = machine.Networks.FirstOrDefault(x => x.AdapterName == message.ChangedNetwork.AdapterName);
            if (network == null)
            {
                network = new MachineNetwork {AdapterName = message.ChangedNetwork.AdapterName};
                machine.Networks.Add(network);
            }

            network.DnsServerAddresses = message.ChangedNetwork.DnsServers;
            network.IpV4Addresses = message.ChangedNetwork.IPAddresses.Select(IPAddress.Parse)
                .Where(n => n.AddressFamily == AddressFamily.InterNetwork)
                .Select(n => n.ToString()).ToArray();
            network.IpV6Addresses = message.ChangedNetwork.IPAddresses.Select(IPAddress.Parse)
                .Where(n => n.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(n => n.ToString()).ToArray();
            network.IPv4DefaultGateway = message.ChangedNetwork.DefaultGateways.Select(IPAddress.Parse)
                .FirstOrDefault(n => n.AddressFamily == AddressFamily.InterNetwork)?.ToString();
            network.IPv6DefaultGateway = message.ChangedNetwork.DefaultGateways.Select(IPAddress.Parse)
                .FirstOrDefault(n => n.AddressFamily == AddressFamily.InterNetworkV6)?.ToString();
            network.IpV4Subnets = message.ChangedNetwork.IPAddresses.Select(IPNetwork.Parse)
                .Where(n => n.AddressFamily == AddressFamily.InterNetwork)
                .Select(n => n.ToString()).ToArray();

            network.IpV6Subnets = message.ChangedNetwork.IPAddresses.Select(IPNetwork.Parse)
                .Where(n => n.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(n => n.ToString()).ToArray();
            await _stateStoreContext.SaveChangesAsync().ConfigureAwait(false);

        }

        private static MachineStatus MapVmStatusToMachineStatus(VmStatus status)
        {
            switch (status)
            {
                case VmStatus.Stopped:
                    return MachineStatus.Stopped;
                case VmStatus.Running:
                    return MachineStatus.Running;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}