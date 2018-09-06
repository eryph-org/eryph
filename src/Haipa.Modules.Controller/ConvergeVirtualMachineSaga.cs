using System;
using System.Linq;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.StateDb;
using HyperVPlus.StateDb.Model;
using JetBrains.Annotations;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Haipa.Modules.Controller
{

    [UsedImplicitly]
    internal class ConvergeVirtualMachineSaga : Saga<CollectLegalInfoSagaData>,
        IAmInitiatedBy<InitiateVirtualMachineConvergeCommand>,
        //IHandleMessages<OperationTimeoutMessage>,
        IHandleMessages<ConvergeVirtualMachineProgressEvent>,
        IHandleMessages<VirtualMachineConvergedEvent>,
        IHandleMessages<AttachMachineToOperationCommand>

    {
        private readonly IBus _bus;
        private readonly StateStoreContext _stateStoreContext;

        public ConvergeVirtualMachineSaga(IBus bus, StateStoreContext stateStoreContext)
        {
            _bus = bus;
            _stateStoreContext = stateStoreContext;
        }

        protected override void CorrelateMessages(ICorrelationConfig<CollectLegalInfoSagaData> config)
        {
            // ensure impotency by setting up correlation for this one in addition to
            // allowing CustomerCreated to initiate a new saga instance
            config.Correlate<InitiateVirtualMachineConvergeCommand>(m => m.ConvergeProcessId, d => d.CorrelationId);

            // ensure proper correlation for the other messages
            //config.Correlate<OperationTimeoutMessage>(m => m.CorrelationId, d => d.CorrelationId);
            config.Correlate<ConvergeVirtualMachineProgressEvent>(m => m.CorellationId, d => d.CorrelationId);
            config.Correlate<AttachMachineToOperationCommand>(m => m.OperationId, d => d.CorrelationId);
            config.Correlate<VirtualMachineConvergedEvent>(m => m.CorellationId, d => d.CorrelationId);

        }

        public async Task Handle(InitiateVirtualMachineConvergeCommand message)
        {
            if (!IsNew)
                return;
            
            //await _bus.DeferLocal(new TimeSpan(0, 0, 10),
            //    new TimeoutMessage {CorrelationId = message.ConvergeProcessId});

            await _bus.Advanced.Routing.Send("haipa.agent.localhost",
                new ConvergeVirtualMachineRequestedEvent
                {
                    Config = message.Config.VirtualMachine,
                    CorellationId = message.ConvergeProcessId,
                }).ConfigureAwait(false);

        }


        public Task Handle(OperationTimeoutMessage message)
        {
            Console.WriteLine("Failed");

            MarkAsComplete();
            return Task.CompletedTask;
        }

        public Task Handle(ConvergeVirtualMachineProgressEvent message)
        {
            var operation = _stateStoreContext.Operations.FirstOrDefault(op => op.Id == message.CorellationId);
            if (operation != null)
            {

                var opLogEntry =
                    new OperationLog
                    {
                        Id = Guid.NewGuid(), Message = message.Message, Operation = operation,
                        Timestamp = DateTime.Now
                    };

                _stateStoreContext.Add(opLogEntry);
                _stateStoreContext.SaveChanges();
            }

            Console.WriteLine(message.Message);
            return Task.CompletedTask;
        }

        public async Task Handle(AttachMachineToOperationCommand message)
        {
            var operation = _stateStoreContext.Operations.FirstOrDefault(op => op.Id == message.OperationId);
            if (operation != null)
            {
                operation.MachineGuid = message.MachineId;
            }

            var agent = _stateStoreContext.Agents.FirstOrDefault(op => op.Name == message.AgentName);
            if (agent == null)
            {
                agent = new Agent {Name = message.AgentName};
                await _stateStoreContext.AddAsync(agent).ConfigureAwait(false);
            }

            var machine = _stateStoreContext.Machines.FirstOrDefault(op => op.Id == message.MachineId);
            if (machine == null)
            {
                machine = new Machine
                {
                    Agent = agent,
                    AgentName = agent.Name,
                    Id = message.MachineId,                    
                };
                await _stateStoreContext.AddAsync(machine).ConfigureAwait(false);

            }
            
            await _stateStoreContext.SaveChangesAsync().ConfigureAwait(false);


            Data.VirtualMaschineId = message.MachineId;
            Data.AgentName = message.AgentName;
            await CheckForComplete().ConfigureAwait(false);

        }

        public async Task Handle(VirtualMachineConvergedEvent message)
        {
            var machine = _stateStoreContext.Machines.SingleOrDefault(op => op.Id == message.Inventory.Id );

            if (machine == null)
            {
                machine = new Machine
                {
                    Id = message.Inventory.Id,
                };
            }

            machine.Name = message.Inventory.Name;

            machine.Status = MapVmStatusToMachineStatus(message.Inventory.Status);
            machine.IpV4Addresses = message.Inventory.IpV4Addresses.Select(
                a => new IpV4Address
                {
                    Address = a
                }).ToList();

            machine.IpV6Addresses = message.Inventory.IpV6Addresses.Select(
                a => new IpV6Address
                {
                    Address = a
                }).ToList();
            
            await _stateStoreContext.SaveChangesAsync().ConfigureAwait(false);

            Data.InventoryReceived = true;
            await CheckForComplete().ConfigureAwait(false);
        }

        private async Task CheckForComplete()
        {
            if (Data.InventoryReceived && Data.VirtualMaschineId != Guid.Empty && Data.AgentName != null)
            {

                await Handle(new ConvergeVirtualMachineProgressEvent {Message = "Converged",
                    CorellationId = Data.CorrelationId});

                MarkAsComplete();
            }
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