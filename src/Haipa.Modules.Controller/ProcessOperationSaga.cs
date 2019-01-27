using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller
{
    [UsedImplicitly]
    internal class ProcessOperationSaga : Saga<OperationSagaData>,
        IAmInitiatedBy<StartOperation>,
        IHandleMessages<OperationAcceptedEvent>,
        IHandleMessages<OperationCompletedEvent>,
        IHandleMessages<OperationFailedEvent>,
        IHandleMessages<OperationTimeoutMessage>,
        IHandleMessages<ConvergeVirtualMachineProgressEvent>,
        IHandleMessages<AttachMachineToOperationCommand>
    {
        private readonly StateStoreContext _dbContext;
        private readonly IBus _bus;

        public ProcessOperationSaga(StateStoreContext dbContext, IBus bus)
        {
            _dbContext = dbContext;
            _bus = bus;
        }


        protected override void CorrelateMessages(ICorrelationConfig<OperationSagaData> config)
        {
            config.Correlate<StartOperation>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTimeoutMessage>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationAcceptedEvent>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationCompletedEvent>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationFailedEvent>(m => m.OperationId, d => d.OperationId);
            config.Correlate<ConvergeVirtualMachineProgressEvent>(m => m.OperationId, d => d.OperationId);
            config.Correlate<AttachMachineToOperationCommand>(m => m.OperationId, d => d.OperationId);

        }

        public async Task Handle(StartOperation message)
        {
            var command = JsonConvert.DeserializeObject(message.CommandData, Type.GetType(message.CommandType));

            var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);
            if (op == null)
            {
                MarkAsComplete();
                return;
            }

            op.Name = command.GetType().Name;

            if (command is IMachineCommand machineCommand)
            {
                var machine = await _dbContext.Machines.FindAsync(machineCommand.MachineId).ConfigureAwait(false);

                if (machine == null)
                {
                    if (command is IOptionalMachineCommand)
                    {
                        await Handle(new OperationCompletedEvent {OperationId = message.OperationId});
                    }
                    else
                    {
                        await Handle(new OperationFailedEvent()
                        {
                            OperationId = message.OperationId, ErrorMessage = "Machine not found"
                        });

                    }
                    return;
                }

                await _bus.Advanced.Routing.Send($"haipa.agent.{machine.AgentName}", command).ConfigureAwait(false);
                await _dbContext.SaveChangesAsync().ConfigureAwait(false);

                return;
            }

            await _bus.Advanced.Topics.Publish("agent.all", command) .ConfigureAwait(false);

        }


        public async Task Handle(OperationTimeoutMessage message)
        {
        }

        public Task Handle(ConvergeVirtualMachineProgressEvent message)
        {
            var operation = _dbContext.Operations.FirstOrDefault(op => op.Id == message.OperationId);
            if (operation != null)
            {

                var opLogEntry =
                    new OperationLog
                    {
                        Id = Guid.NewGuid(),
                        Message = message.Message,
                        Operation = operation,
                        Timestamp = DateTime.Now
                    };

                _dbContext.Add(opLogEntry);
                _dbContext.SaveChanges();
            }

            Console.WriteLine(message.Message);
            return Task.CompletedTask;
        }


        public async Task Handle(OperationAcceptedEvent message)
        {
            var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);

            if (op == null)
                return;

            op.Status = OperationStatus.Running;
            op.StatusMessage = OperationStatus.Running.ToString();
            op.AgentName = message.AgentName;

            await _dbContext.SaveChangesAsync();

        }

        public async Task Handle(OperationCompletedEvent message)
        {
            var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);

            if (op == null)
                return;

            op.Status = OperationStatus.Completed;
            op.StatusMessage = OperationStatus.Completed.ToString();
            await _dbContext.SaveChangesAsync();
            await _bus.Advanced.Topics.Publish("agent.all", new InventoryRequestedEvent()).ConfigureAwait(false);
            MarkAsComplete();
        }

        public async Task Handle(OperationFailedEvent message)
        {
            var op = await _dbContext.Operations.FindAsync(message.OperationId).ConfigureAwait(false);

            if (op == null)
                return;

            op.Status = OperationStatus.Failed;
            op.StatusMessage = message.ErrorMessage;

            await _dbContext.SaveChangesAsync();

            MarkAsComplete();
        }

        public async Task Handle(AttachMachineToOperationCommand message)
        {
            var operation = _dbContext.Operations.FirstOrDefault(op => op.Id == message.OperationId);
            if (operation != null)
            {
                operation.MachineGuid = message.MachineId;
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
                    VM = new VirtualMachine{ Id = message.MachineId}
                };
                await _dbContext.AddAsync(machine).ConfigureAwait(false);

            }

            await _dbContext.SaveChangesAsync().ConfigureAwait(false);

        }

    }
}