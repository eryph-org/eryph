﻿using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.StateDb;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Inventory
{
    [UsedImplicitly]
    internal class VirtualMachineIpAddressChangedEventHandler : IHandleMessages<VirtualMachineNetworkChangedEvent>
    {
        private readonly StateStoreContext _stateStoreContext;

        public VirtualMachineIpAddressChangedEventHandler(StateStoreContext stateStoreContext)
        {
            _stateStoreContext = stateStoreContext;
        }

        public async Task Handle(VirtualMachineNetworkChangedEvent message)
        {
            //var machine = await _stateStoreContext.VirtualCatlets
            //    .Include(vm => vm.NetworkAdapters)
            //    //.Include(x => x.Networks)
            //    .FirstOrDefaultAsync(x => x.VMId == message.VMId);

            //var adapter = machine?.NetworkAdapters
            //    .FirstOrDefault(a => a.Name == message.ChangedAdapter.AdapterName);

            //if (adapter == null)
            //{
            //    adapter = new VirtualCatletNetworkAdapter
            //        {Id = message.ChangedAdapter.Id, Name = message.ChangedAdapter.AdapterName, Vm = machine};
            //    _stateStoreContext.Add(adapter);
            //}

            //adapter.SwitchName = message.ChangedAdapter.VirtualSwitchName;

            //var network = machine?.Networks.FirstOrDefault(x =>
            //    x.Name == message.ChangedNetwork.Name);

            //if (network == null)
            //{
            //    network = new MachineNetwork {Id = Guid.NewGuid(), Name = message.ChangedNetwork.Name};
            //    machine?.Networks.Add(network);
            //}

            //network.DnsServerAddresses = message.ChangedNetwork.DnsServers;
            //network.IpV4Addresses = message.ChangedNetwork.IPAddresses.Select(IPAddress.Parse)
            //    .Where(n => n.AddressFamily == AddressFamily.InterNetwork)
            //    .Select(n => n.ToString()).ToArray();
            //network.IpV6Addresses = message.ChangedNetwork.IPAddresses.Select(IPAddress.Parse)
            //    .Where(n => n.AddressFamily == AddressFamily.InterNetworkV6)
            //    .Select(n => n.ToString()).ToArray();
            //network.IPv4DefaultGateway = message.ChangedNetwork.DefaultGateways.Select(IPAddress.Parse)
            //    .FirstOrDefault(n => n.AddressFamily == AddressFamily.InterNetwork)?.ToString();
            //network.IPv6DefaultGateway = message.ChangedNetwork.DefaultGateways.Select(IPAddress.Parse)
            //    .FirstOrDefault(n => n.AddressFamily == AddressFamily.InterNetworkV6)?.ToString();
            //network.IpV4Subnets = message.ChangedNetwork.IPAddresses.Select(IPNetwork.Parse)
            //    .Where(n => n.AddressFamily == AddressFamily.InterNetwork)
            //    .Select(n => n.ToString()).ToArray();

            //network.IpV6Subnets = message.ChangedNetwork.IPAddresses.Select(IPNetwork.Parse)
            //    .Where(n => n.AddressFamily == AddressFamily.InterNetworkV6)
            //    .Select(n => n.ToString()).ToArray();
        }
    }
}