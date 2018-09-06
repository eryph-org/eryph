using System;
using System.Linq;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.StateDb;
using HyperVPlus.StateDb.Model;
using Rebus.Handlers;

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
            var agentData = _stateStoreContext.Agents.FirstOrDefault(x=> x.Name == message.AgentName);

            if (agentData == null)
            {
                agentData = new Agent{ Name= message.AgentName };
            }
            else
            {
                _stateStoreContext.RemoveRange(agentData.Machines);
            }

            
            await _stateStoreContext.AddRangeAsync(message.Inventory.Select(x=>
            {
                return new Machine
                {
                    Id = x.Id,
                    Name = x.Name,
                    Status = MapVmStatusToMachineStatus(x.Status),
                    Agent = agentData,
                    IpV4Addresses = x.IpV4Addresses?.Select(
                        a => new IpV4Address
                        {
                            Address = a
                        }).ToList(),
                    IpV6Addresses = x.IpV6Addresses?.Select(
                        a => new IpV6Address
                        {
                            Address = a
                        }).ToList()
                };
            })).ConfigureAwait(false);

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