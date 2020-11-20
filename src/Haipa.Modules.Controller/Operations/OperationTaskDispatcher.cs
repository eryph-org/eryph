using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haipa.Messages.Operations;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Haipa.VmConfig;
using Newtonsoft.Json;
using Rebus.Bus;

namespace Haipa.Modules.Controller.Operations
{
    class OperationTaskDispatcher : IOperationTaskDispatcher
    {
        private readonly IBus _bus;
        private readonly StateStoreContext _stateStoreContext;

        public OperationTaskDispatcher(IBus bus, StateStoreContext stateStoreContext)
        {
            _bus = bus;
            _stateStoreContext = stateStoreContext;
        }

        public Task StartNewOperation(OperationTaskCommand message, long? resourceId, ResourceType? resourceType)
        {

            var operation = new Operation
            {
                Id = message.OperationId,
                Status = OperationStatus.Queued,
                Resources = resourceId.HasValue ? new List<OperationResource>{new OperationResource
                {
                    Id = Guid.NewGuid(), ResourceId = resourceId.GetValueOrDefault(), ResourceType = resourceType.GetValueOrDefault()
                }} : null
            };

            _stateStoreContext.Add(operation);

            var commandJson = JsonConvert.SerializeObject(message);

            return _bus.SendLocal(
                new CreateOperationCommand
                {
                    TaskMessage = new CreateNewOperationTaskCommand(
                        message.GetType().AssemblyQualifiedName,
                        commandJson, operation.Id,
                        message.TaskId)
                });
        }

        public Task Send(OperationTaskCommand message)
        {
            var commandJson = JsonConvert.SerializeObject(message);

            return _bus.SendLocal(
                new CreateNewOperationTaskCommand(
                    message.GetType().AssemblyQualifiedName,
                    commandJson, message.OperationId,
                    message.TaskId));
        }
    }
}