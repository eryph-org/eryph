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

namespace Eryph.ModuleCore;

public abstract class OperationOrTaskDispatcherBase
{

    private readonly IBus _bus;
    private readonly ILogger<OperationOrTaskDispatcherBase> _logger;
    private readonly StateStoreContext _db;

    protected OperationOrTaskDispatcherBase(IBus bus, 
        ILogger<OperationOrTaskDispatcherBase> logger, StateStoreContext db)
    {
        _bus = bus;
        _logger = logger;
        _db = db;
    }


    protected async Task<IEnumerable<Operation>> StartOpOrTask(Guid tenantId, Guid operationId, 
        Guid initiatingTaskId, object command,
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

        var projects = new List<Guid>();

        if (command is IHasProjectId hasProjectId)
            projects.Add(hasProjectId.ProjectId);


        // resources can be set directly or by command
        // at end we join the resources into command but to ensure that all are handled
        // try to collect them from command, too
        resources ??= Array.Empty<Resource>();
        var resourcesCommand = command as IResourcesCommand;
        var resourceCommand = command as IResourceCommand;

        if (resourcesCommand is { Resources: { } }) resources = resources.Concat(resourcesCommand.Resources).ToArray();
        if (resourceCommand != null && resourceCommand.Resource.Id != Guid.Empty)
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
        if (taskCount > 1 && resourcesCommand != null)
            taskCount = 1;

        for (var i = 0; i < taskCount; i++)
        {
            if (!existingOperation)
            {
                var resourceIds = resources.Select(x => x.Id);
                var projectsOfResources = await _db.Resources
                    .Where(x => resourceIds.Contains(x.Id)).Select(x => x.ProjectId)
                    .Distinct()
                    .ToArrayAsync();
                projects.AddRange(projectsOfResources);
                projects = projects.Distinct().ToList();

                var operation = new Operation
                {
                    Id = operationId,
                    Status = OperationStatus.Queued,
                    Resources = resources.Select(x => new OperationResource
                            { ResourceId = x.Id, ResourceType = x.Type })
                        .ToList(),
                    Projects = projects.Select(p => new OperationProject { Id = Guid.NewGuid(), ProjectId = p })
                        .ToList(),
                    TenantId = tenantId
                };



                _db.Add(operation);
                result.Add(operation);

                await _db.SaveChangesAsync();
            }
            else
            {
                var operation = await _db.Operations.Include(x => x.Resources)
                    .FirstOrDefaultAsync(x => x.Id == operationId);

                if (operation == null)
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

                var resourceIds = resources.Select(x => x.Id)
                    .Append(operation.Resources.Select(x => x.ResourceId))
                    .Distinct();

                var projectsOfResources = await _db.Resources
                    .Where(x => resourceIds.Contains(x.Id)).Select(x => x.ProjectId)
                    .Distinct()
                    .ToArrayAsync();
                projects.AddRange(projectsOfResources);
                projects = projects.Distinct().ToList();

                //operation.Projects = projects.Select(p => new OperationProject
                //        { Id = Guid.NewGuid(), ProjectId = p })
                //    .ToList();

                _db.Update(operation);
                await _db.SaveChangesAsync();

            }


            if (resourcesCommand != null)
            {
                resourcesCommand.Resources = resources;
            }

            if (resourceCommand != null && resources.Length > i)
            {
                var resource = resources[i];
                resourceCommand.Resource = resource;
            }

            if (initiatingTaskId == Guid.Empty)
                initiatingTaskId = operationId;

            var commandJson = JsonSerializer.Serialize(command);
            var taskMessage = new CreateNewOperationTaskCommand(
                command.GetType().AssemblyQualifiedName,
                commandJson,
                operationId,
                initiatingTaskId,
                Guid.NewGuid());

            object message;

            if (!existingOperation)
                message = new CreateOperationCommand { TaskMessage = taskMessage };
            else
                message = taskMessage;


            await _bus.Send(message);

            _logger.LogInformation($"Send new command of type {command.GetType()} to controllers. Id: {operationId}");
        }


        return result;
    }

}