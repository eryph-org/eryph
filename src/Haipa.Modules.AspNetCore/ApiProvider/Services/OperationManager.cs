using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haipa.Messages.Commands;
using Haipa.Messages.Operations;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Haipa.VmConfig;
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
            return StartNew<T>(null, null);
        }

        public Task<Operation> StartNew<T>(long? resourceId, ResourceType? resourceType) where T : OperationTaskCommand
        {
            return StartNew(Activator.CreateInstance<T>(), resourceId, resourceType);
        }

        public Task<Operation> StartNew(Type operationCommandType)
        {
            return StartNew(operationCommandType, null, null);
        }

        public Task<Operation> StartNew(Type operationCommandType, long? resourceId, ResourceType? resourceType)
        {
            return StartNew(Activator.CreateInstance(operationCommandType) as OperationTaskCommand, resourceId, resourceType);
        }

        public Task<Operation> StartNew(OperationTaskCommand operationCommand)
        {
            return StartNew(operationCommand,null, null);
        }

        public async Task<Operation> StartNew(OperationTaskCommand taskCommand, long? resourceId, ResourceType? resourceType)
        {
            if(taskCommand == null)
                throw new ArgumentNullException(nameof(taskCommand));
          
            var operation = new Operation
            {
                Id = Guid.NewGuid(),
                Status = OperationStatus.Queued,
                Resources = resourceId.HasValue ? new List<OperationResource>{new OperationResource
                {
                    Id = Guid.NewGuid(), ResourceId = resourceId.GetValueOrDefault(), ResourceType = resourceType.GetValueOrDefault()
                }}: null
            };

            _db.Add(operation);

            await _db.SaveChangesAsync().ConfigureAwait(false);
        
            if (resourceId != 0 && (taskCommand is IResourceCommand resourceCommand) 
                                         && resourceCommand.ResourceId != resourceId)
                resourceCommand.ResourceId = resourceId.GetValueOrDefault(0);


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