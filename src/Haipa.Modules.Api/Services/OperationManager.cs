using System;
using System.Threading.Tasks;
using HyperVPlus.Messages;
using HyperVPlus.StateDb;
using HyperVPlus.StateDb.Model;
using Newtonsoft.Json;
using Rebus.Bus;

namespace Haipa.Modules.Api.Services
{
    class OperationManager : IOperationManager
    {
        private readonly StateStoreContext _db;
        private readonly IBus _bus;


        public OperationManager(StateStoreContext db, IBus bus)
        {
            _db = db;
            _bus = bus;
        }

        public Task<Operation> StartNew<T>() where T : OperationCommand
        {
            return StartNew<T>(Guid.Empty);
        }

        public Task<Operation> StartNew<T>(Guid vmId) where T : OperationCommand
        {
            return StartNew(Activator.CreateInstance<T>(), vmId);
        }

        public Task<Operation> StartNew(Type operationCommandType)
        {
            return StartNew(operationCommandType, Guid.Empty);
        }

        public Task<Operation> StartNew(Type operationCommandType, Guid vmId)
        {
            return StartNew(Activator.CreateInstance(operationCommandType) as OperationCommand, vmId);
        }

        public Task<Operation> StartNew(OperationCommand operationCommand)
        {
            return StartNew(operationCommand, Guid.Empty);
        }

        public async Task<Operation> StartNew(OperationCommand operationCommand, Guid vmId)
        {
            if(operationCommand == null)
                throw new ArgumentNullException(nameof(operationCommand));
          
            var operation = new Operation
            {
                Id = Guid.NewGuid(),
                MachineGuid = vmId,
                Status = OperationStatus.Queued
            };
            _db.Add(operation);

            await _db.SaveChangesAsync().ConfigureAwait(false);
        
            if (vmId != Guid.Empty && (operationCommand is IMachineCommand machineCommand) 
                                   && machineCommand.MachineId != vmId)
                machineCommand.MachineId = vmId;


            operationCommand.OperationId = operation.Id;
            var commandJson = JsonConvert.SerializeObject(operationCommand);

            await _bus.Send(
                new StartOperation(operationCommand.GetType().AssemblyQualifiedName, commandJson, operation.Id)
                ).ConfigureAwait(false);

            return operation;
        }


    }
}