using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Rebus.Handlers;

namespace Haipa.Modules.Controller.Operations
{
    [UsedImplicitly]
    public class AttachMachineToOperationCommandHandler : IHandleMessages<AttachMachineToOperationCommand>
    {
        private readonly StateStoreContext _dbContext;

        public AttachMachineToOperationCommandHandler(StateStoreContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task Handle(AttachMachineToOperationCommand message)
        {
            var operation = _dbContext.Operations.Include(x=>x.Resources)
                .FirstOrDefault(op => op.Id == message.OperationId);

            if (operation != null)
            {
                if(operation.Resources.All(x => x.ResourceId != message.MachineId))
                    operation.Resources.Add(new OperationResource
                    {
                        Id = Guid.NewGuid(),
                        ResourceId = message.MachineId, 
                        ResourceType = ResourceType.Machine
                    });
            }

            var agent = _dbContext.Agents.FirstOrDefault(op => op.Name == message.AgentName);
            if (agent == null)
            {
                agent = new Agent { Name = message.AgentName };
                await _dbContext.AddAsync(agent).ConfigureAwait(false);
            }

            var machine = _dbContext.Machines.FirstOrDefault(op => op.Id == message.MachineId);
            if (machine == null)
            {
                machine = new Machine
                {
                    Agent = agent,
                    AgentName = agent.Name,
                    Id = message.MachineId,
                    VM = new VirtualMachine { Id = message.MachineId }
                };
                await _dbContext.AddAsync(machine).ConfigureAwait(false);

            }

            await _dbContext.SaveChangesAsync().ConfigureAwait(false);

        }

    }
}