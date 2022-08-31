using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Operations.Commands;
using Eryph.Messages.Resources;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Resource = Eryph.Resources.Resource;

namespace Eryph.ModuleCore
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

        public Task<IEnumerable<Operation>> StartNew<T>(params Resource[] resources)
            where T : class, new()
        {
            return StartNew(Guid.Empty,Activator.CreateInstance<T>(), resources);
        }

        public async Task<Operation?> StartNew(Type commandType, Resource resource = default)
        {
            return (await StartNew(commandType, new[] { resource }))?.FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew(Type commandType, params Resource[] resources)
        {
            return StartNew(Guid.Empty, Activator.CreateInstance(commandType) ?? throw new InvalidOperationException(), resources);
        }
        

        public Task<IEnumerable<Operation>> StartNew(object command, params Resource[] resources)
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

            // resources can be set directly or by command
            // at end we join the resources into command but to ensure that all are handled
            // try to collect them from command, too
            resources ??= Array.Empty<Resource>();
            var resourcesCommand = command as IResourcesCommand;
            var resourceCommand = command as IResourceCommand;

            if (resourcesCommand is { Resources: { } }) resources = resources.Concat(resourcesCommand.Resources).ToArray();
            if (resourceCommand != null && resourceCommand.Resource.Id!=Guid.Empty) 
                resources = resources.Concat(new[] { resourceCommand.Resource }).ToArray();

            resources = resources.Distinct().ToArray();

            // if supported by the command use the correlation id as operation id
            // this will prevent that commands are send twice
            if (command is IHasCorrelationId correlatedCommand)
                operationId = correlatedCommand.CorrelationId == Guid.Empty
                    ? operationId
                    : correlatedCommand.CorrelationId;

            var result = new List<Operation>();

            //create a task for each resource
            //or, if command supports multiple resources or there are no resources - 1 task
            var taskCount = resources.Length == 0 ? 1 : resources.Length;
            if (taskCount > 1 && resourcesCommand!=null)
                taskCount = 1;

            for (var i = 0; i < taskCount; i++)
            {
                if (!existingOperation)
                {

                    var operation = new Operation
                    {
                        Id = operationId,
                        Status = OperationStatus.Queued,
                        Resources = resources.Distinct().Select(x => new OperationResource
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

                    foreach (var resource in resources)
                    {
                        if (!operation.Resources.Any(
                                x => x.ResourceType == resource.Type && x.ResourceId == resource.Id))
                        {
                            operation.Resources.Add(new OperationResource
                            {
                                ResourceId = resource.Id,
                                ResourceType = resource.Type
                            });
                        }
                    }


                    _db.Update(operation);
                    await _db.SaveChangesAsync();

                }


                if (resourcesCommand!= null)
                {
                    resourcesCommand.Resources = resources;
                }

                if (resourceCommand != null && resources.Length > i)
                {
                    var resource = resources[i];
                    resourceCommand.Resource = resource;
                }

                var commandJson = JsonSerializer.Serialize(command);
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

            return (await StartNew(operationId, Activator.CreateInstance<T>(), resource)).FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew<T>(Guid operationId, [AllowNull] params Resource[] resources)
            where T : class, new()
        {
            if (operationId == Guid.Empty)
                throw new ArgumentException("Invalid empty operation id", nameof(operationId));

            return StartNew(operationId, Activator.CreateInstance<T>(), resources);
        }

        public async Task<Operation?> StartNew(Guid operationId, Type operationCommandType, Resource resource = default)
        {
            if (operationId == Guid.Empty)
                throw new ArgumentException("Invalid empty operation id", nameof(operationId));

            return (await StartNew(operationId, operationCommandType, new[] { resource }))?.FirstOrDefault();
        }

        public Task<IEnumerable<Operation>> StartNew(Guid operationId, Type commandType, [AllowNull] params Resource[] resources)
        {
            if (operationId == Guid.Empty)
                throw new ArgumentException("Invalid empty operation id", nameof(operationId));

            return StartNew(operationId, Activator.CreateInstance(commandType), resources);
        }

    }
}