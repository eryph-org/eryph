using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haipa.Messages.Commands;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Messages.Operations;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Newtonsoft.Json;
using Rebus.Bus;

namespace Haipa.Modules.AspNetCore.ApiProvider.Services
{
    public class OperationManager : IOperationManager
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

        public async Task<Operation> StartNew(OperationTaskCommand taskCommand, Guid resourceId)
        {
            if(taskCommand == null)
                throw new ArgumentNullException(nameof(taskCommand));
          
            // if supported by the command use the correlation id as operation id
            // this will prevent that commands are send twice
            var operationId = Guid.NewGuid();
            if (taskCommand is IHasCorrelationId correlatedCommand)
                operationId = correlatedCommand.CorrelationId!=Guid.Empty ? operationId : correlatedCommand.CorrelationId;


            var operation = new Operation
            {
                Id = operationId,
                Status = OperationStatus.Queued,
                Resources = new List<OperationResource>{new OperationResource
                {
                    Id = Guid.NewGuid(), ResourceId = resourceId, ResourceType = ResourceType.Machine
                }}
            };

            _db.Add(operation);

            await _db.SaveChangesAsync().ConfigureAwait(false);
        
            if (resourceId != Guid.Empty && (taskCommand is IMachineCommand machineCommand) 
                                         && machineCommand.MachineId != resourceId)
                machineCommand.MachineId = resourceId;


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