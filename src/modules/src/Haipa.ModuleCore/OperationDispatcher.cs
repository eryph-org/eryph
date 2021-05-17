using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Messages.Operations.Commands;
using Haipa.Messages.Resources;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rebus.Bus;
using Resource = Haipa.Resources.Resource;

namespace Haipa.ModuleCore
{
    public class OperationDispatcher : IOperationDispatcher, IOperationTaskDispatcher
    {
        private readonly IBus _bus;
        private readonly ILogger<OperationDispatcher> _logger;
        private readonly StateStoreContext _db;


        public OperationDispatcher(StateStoreContext db, IBus bus, ILogger<OperationDispatcher> logger)
        {
            _db = db;
            _bus = bus;
            _logger = logger;
        }

        public async Task<Operation?> StartNew(object command)
        {
            return ( await StartNew(Guid.Empty, command, null)).FirstOrDefault();
        }

        public async Task<Operation?> StartNew<T>(Resource resource = default) 
            where T : class, new()
        {
            return (await StartNew(Guid.Empty,Activator.CreateInstance<T>(), resource)).FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew<T>([AllowNull] params Resource[] resources)
            where T : class, new()
        {
            return StartNew(Guid.Empty,Activator.CreateInstance<T>(), resources);
        }

        public async Task<Operation?> StartNew(Type commandType, Resource resource = default)
        {
            return (await StartNew(commandType, new[] { resource }))?.FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew(Type commandType, [AllowNull] params Resource[] resources)
        {
            return StartNew(Guid.Empty, Activator.CreateInstance(commandType), resources);
        }
        

        public Task<IEnumerable<Operation>> StartNew(object command,
            [AllowNull] params Resource[] resources)
        {
            return StartNew(Guid.Empty, command, resources);

        }

        public async Task<IEnumerable<Operation>> StartNew(Guid operationId, object command,
            [AllowNull] params Resource[] resources)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            bool existingOperation = true;
            if (operationId == Guid.Empty)
            {
                existingOperation = false;
                operationId = Guid.NewGuid();
            }
                

            // if supported by the command use the correlation id as operation id
            // this will prevent that commands are send twice
            if (command is IHasCorrelationId correlatedCommand)
                operationId = correlatedCommand.CorrelationId == Guid.Empty
                    ? operationId
                    : correlatedCommand.CorrelationId;

            var result = new List<Operation>();

            //create a operation for each resource
            //or, if command supports multiple resources or there are no resources - 1 operation
            var opCount = (resources?.Length).GetValueOrDefault(1);
            if (opCount > 1 && command is IResourcesCommand)
                opCount = 1;

            for (var i = 0; i < opCount; i++)
            {
                if (!existingOperation)
                {

                    var operation = new Operation
                    {
                        Id = operationId,
                        Status = OperationStatus.Queued,
                        Resources = resources?.Select(x => new OperationResource
                                {ResourceId = x.Id, ResourceType = x.Type})
                            .ToList()
                    };

                    _db.Add(operation);
                    result.Add(operation);

                    await _db.SaveChangesAsync();
                }
                else
                {
                    var operation = await _db.Operations.Include(x => x.Resources)
                        .FirstOrDefaultAsync(x => x.Id == operationId);

                    if(operation == null)
                        throw new InvalidOperationException($"Could not find operation {operationId} in state db.");

                    operation.Resources = operation.Resources?.Concat(resources?.Select(x => new OperationResource
                        {ResourceId = x.Id, ResourceType = x.Type}) ?? Array.Empty<OperationResource>())
                        .Distinct().ToList();

                    _db.Update(operation);
                    await _db.SaveChangesAsync();

                }


                if (command is IResourcesCommand resourcesCommand)
                {
                    resourcesCommand.Resources = resources;
                }
                else
                {
                    var resource = resources?[i];
                    if (resource.HasValue && command is IResourceCommand resourceCommand)
                        resourceCommand.Resource = resource.Value;
                }

                var commandJson = JsonConvert.SerializeObject(command);
                var taskMessage = new CreateNewOperationTaskCommand(
                    command.GetType().AssemblyQualifiedName,
                    commandJson,
                    operationId,
                    Guid.NewGuid());

                object message;

                if (!existingOperation)
                    message = new CreateOperationCommand {TaskMessage = taskMessage};
                else
                    message = taskMessage;


                await _bus.Send(message);

                _logger.LogInformation($"Send new command of type {command.GetType()} to controllers. Id: {operationId}");
            }


            return result;
        }


        public async Task<Operation?> StartNew(Guid operationId, object command)
        {
            if (operationId == Guid.Empty)
                throw new ArgumentException("Invalid empty operation id", nameof(operationId));

            return (await StartNew(operationId, command, null)).FirstOrDefault();
        }


        public async Task<Operation?> StartNew<T>(Guid operationId, Resource resource = default) where T : class, new()
        {
            if(operationId == Guid.Empty)
                throw new ArgumentException("Invalid empty operation id", nameof(operationId));

            return (await StartNew(Guid.Empty, Activator.CreateInstance<T>(), resource)).FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew<T>(Guid operationId, [AllowNull] params Resource[] resources)
            where T : class, new()
        {
            if (operationId == Guid.Empty)
                throw new ArgumentException("Invalid empty operation id", nameof(operationId));

            return StartNew(Guid.Empty, Activator.CreateInstance<T>(), resources);
        }

        public async Task<Operation?> StartNew(Guid operationId, Type operationCommandType, Resource resource = default)
        {
            if (operationId == Guid.Empty)
                throw new ArgumentException("Invalid empty operation id", nameof(operationId));

            return (await StartNew(operationCommandType, new[] { resource }))?.FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew(Guid operationId, Type commandType, [AllowNull] params Resource[] resources)
        {
            if (operationId == Guid.Empty)
                throw new ArgumentException("Invalid empty operation id", nameof(operationId));

            return StartNew(Guid.Empty, Activator.CreateInstance(commandType), resources);
        }

    }
}