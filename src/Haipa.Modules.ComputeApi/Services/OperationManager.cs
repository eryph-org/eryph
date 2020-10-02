using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Commands;
using Haipa.Messages.Operations;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Newtonsoft.Json;
using Rebus.Bus;
using OperationTask = Haipa.StateDb.Model.OperationTask;

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

        public Task<Operation> StartNew<T>() where T : OperationTaskCommand
        {
            return StartNew<T>(Guid.Empty);
        }

        public Task<Operation> StartNew<T>(Guid vmId) where T : OperationTaskCommand
        {
            return StartNew(Activator.CreateInstance<T>(), vmId);
        }

        public Task<Operation> StartNew(Type operationCommandType)
        {
            return StartNew(operationCommandType, Guid.Empty);
        }

        public Task<Operation> StartNew(Type operationCommandType, Guid vmId)
        {
            return StartNew(Activator.CreateInstance(operationCommandType) as OperationTaskCommand, vmId);
        }

        public Task<Operation> StartNew(OperationTaskCommand operationCommand)
        {
            return StartNew(operationCommand, Guid.Empty);
        }

        public async Task<Operation> StartNew(OperationTaskCommand taskCommand, Guid vmId)
        {
            if(taskCommand == null)
                throw new ArgumentNullException(nameof(taskCommand));
          
            var operation = new Operation
            {
                Id = Guid.NewGuid(),
                Status = OperationStatus.Queued,
                Resources = new List<OperationResource>{new OperationResource
                {
                    Id = Guid.NewGuid(), ResourceId = vmId, ResourceType = ResourceType.Machine
                }}
            };

            _db.Add(operation);

            await _db.SaveChangesAsync().ConfigureAwait(false);
        
            if (vmId != Guid.Empty && (taskCommand is IMachineCommand machineCommand) 
                                   && machineCommand.MachineId != vmId)
                machineCommand.MachineId = vmId;


            taskCommand.OperationId = operation.Id;
            taskCommand.TaskId = Guid.NewGuid();
            var commandJson = JsonConvert.SerializeObject(taskCommand);

            await _bus.Send(
                new CreateOperationCommand{ 
                    TaskMessage = new CreateNewOperationTaskCommand(
                        taskCommand.GetType().AssemblyQualifiedName, 
                        commandJson, operation.Id,
                        taskCommand.TaskId) })
                .ConfigureAwait(false);

            return operation;
        }


    }
}