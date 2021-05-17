using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Operations;
using Haipa.Messages.Operations.Commands;
using Haipa.Messages.Resources;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Newtonsoft.Json;
using Rebus.Bus;
using Resource = Haipa.Primitives.Resources.Resource;

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


        public async Task<Operation?> StartNew<T>(Resource resource = default) where T : OperationTaskCommand
        {
            return (await StartNew(Activator.CreateInstance<T>(), resource)).FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew<T>([AllowNull] params Resource[] resources) where T : OperationTaskCommand
        {
            return StartNew(Activator.CreateInstance<T>(), resources);
        }

        public async Task<Operation?> StartNew(Type operationCommandType, Resource resource = default)
        {
            return (await StartNew(operationCommandType, new []{resource}))?.FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew(Type operationCommandType, [AllowNull] params Resource[] resources)
        {
            if (!(Activator.CreateInstance(operationCommandType) is OperationTaskCommand command))
                throw new ArgumentException("Invalid operation task type", nameof(operationCommandType));

            return StartNew(command, resources);
        }

        public async Task<Operation?> StartNew(OperationTaskCommand operationCommand)
        {
            return (await StartNew(operationCommand,null)).FirstOrDefault();
        }

        public async Task<IEnumerable<Operation>> StartNew(OperationTaskCommand taskCommand, [AllowNull] params Resource[] resources)
        {
            if(taskCommand == null)
                throw new ArgumentNullException(nameof(taskCommand));
          
            // if supported by the command use the correlation id as operation id
            // this will prevent that commands are send twice
            var operationId = Guid.NewGuid();
            if (taskCommand is IHasCorrelationId correlatedCommand)
                operationId = correlatedCommand.CorrelationId!=Guid.Empty ? operationId : correlatedCommand.CorrelationId;

            var result = new List<Operation>();

            //create a operation for each resource
            //or, if command supports multiple resources or there are no resources - 1 operation
            var opCount = (resources?.Length).GetValueOrDefault(1);
            if (opCount > 1 && taskCommand is IResourcesCommand)
                opCount = 1;

            for (var i = 0; i < opCount; i++)
            {

                var operation = new Operation
                {
                    Id = operationId,
                    Status = OperationStatus.Queued,
                    Resources = resources?.Select(x => new OperationResource {ResourceId = x.Id, ResourceType = x.Type})
                        .ToList()
                };

                _db.Add(operation);
                result.Add(operation);

                await _db.SaveChangesAsync();

                if (taskCommand is IResourcesCommand resourcesCommand)
                {
                    resourcesCommand.Resources = resources;
                }
                else
                {
                    var resource = resources?[i];
                    if (resource.HasValue && taskCommand is IResourceCommand resourceCommand)
                        resourceCommand.Resource = resource.Value;
                }

                taskCommand.OperationId = operation.Id;
                taskCommand.TaskId = Guid.NewGuid();
                var commandJson = JsonConvert.SerializeObject(taskCommand);

                await _bus.Send(
                        new CreateOperationCommand
                        {
                            TaskMessage = new CreateNewOperationTaskCommand(
                                taskCommand.GetType().AssemblyQualifiedName,
                                commandJson, operation.Id,
                                taskCommand.TaskId)
                        });
            }


            return result;
        }


    }
}