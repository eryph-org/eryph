using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Rebus.Handlers;
using Rebus.Pipeline;

namespace Haipa.Modules.Controller
{
    internal class UpdateInventoryCommandHandler : IHandleMessages<UpdateInventoryCommand>
    {
        private readonly StateStoreContext _stateStoreContext;

        public UpdateInventoryCommandHandler(StateStoreContext stateStoreContext)
        {
            _stateStoreContext = stateStoreContext;
        }

        public async Task Handle(UpdateInventoryCommand message)
        {
            var agentData = await _stateStoreContext.Agents
                .Include(a=> a.Machines).FirstOrDefaultAsync(x=> x.Name == message.AgentName).ConfigureAwait(false);

            if (agentData == null)
            {
                agentData = new Agent { Name = message.AgentName };
            }

            var newMachines = message.Inventory.Select(x =>
            {
                return new Machine
                {
                    Id = x.MachineId,
                    Name = x.Name,
                    Status = MapVmStatusToMachineStatus(x.Status),
                    Agent = agentData,
                    VM = new VirtualMachine
                    {
                        NetworkAdapters = x.NetworkAdapters.Select(a => new VirtualMachineNetworkAdapter
                        {
                            MachineId = x.MachineId,
                            Name = a.AdapterName,
                            SwitchName = a.VirtualSwitchName                        
                        }).ToList(),                      
                    },
                    Networks = x.Networks?.Select( mn=> new MachineNetwork
                    {
                        MachineId = x.MachineId,
                        AdapterName = mn.AdapterName,
                        DnsServerAddresses = mn.DnsServers,                       
                        IpV4Addresses = mn.IPAddresses.Select(IPAddress.Parse).Where(n=>n.AddressFamily == AddressFamily.InterNetwork )
                                .Select(n=>n.ToString()).ToArray(),
                        IpV6Addresses = mn.IPAddresses.Select(IPAddress.Parse).Where(n=>n.AddressFamily == AddressFamily.InterNetworkV6 )
                            .Select(n=>n.ToString()).ToArray(),
                        IPv4DefaultGateway = mn.DefaultGateways.Select(IPAddress.Parse).FirstOrDefault(n=>n.AddressFamily == AddressFamily.InterNetwork)?.ToString(),
                        IPv6DefaultGateway = mn.DefaultGateways.Select(IPAddress.Parse).FirstOrDefault(n=>n.AddressFamily == AddressFamily.InterNetworkV6)?.ToString(),
                        IpV4Subnets = mn.IPAddresses.Select(IPNetwork.Parse).Where(n=>n.AddressFamily == AddressFamily.InterNetwork )
                            .Select(n=>n.ToString()).ToArray(),
                        IpV6Subnets = mn.IPAddresses.Select(IPNetwork.Parse).Where(n=>n.AddressFamily == AddressFamily.InterNetworkV6 )
                            .Select(n=>n.ToString()).ToArray(),
                    }).ToList()
                };
            });

            foreach (var newMachine in newMachines)
            {
                var existingMachine = await _stateStoreContext.Machines.Where(x=>x.Id == newMachine.Id)
                    .Include(x => x.VM)
                    .ThenInclude(x => x.NetworkAdapters)
                    .Include(x => x.Networks).FirstOrDefaultAsync().ConfigureAwait(false);

                if (existingMachine == null)
                {
                    _stateStoreContext.Add(newMachine);
                    continue;
                }

                existingMachine.Name = newMachine.Name;
                existingMachine.Status = newMachine.Status;
                existingMachine.Agent = agentData;

                if(existingMachine.VM== null)
                    existingMachine.VM = new VirtualMachine();

                existingMachine.VM.NetworkAdapters = newMachine.VM.NetworkAdapters;
                existingMachine.Networks = newMachine.Networks;
            }


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
                case VmStatus.Pending:
                    return MachineStatus.Pending;
                case VmStatus.Error:
                    return MachineStatus.Error;

                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}