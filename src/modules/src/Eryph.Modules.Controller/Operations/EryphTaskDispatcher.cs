using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources;
using Eryph.Resources;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Eryph.Modules.Controller.Operations;

public class EryphTaskDispatcher : DefaultOperationTaskDispatcher
{
    public EryphTaskDispatcher(IBus bus, WorkflowOptions workflowOptions, ILogger<DefaultOperationTaskDispatcher> logger, IOperationManager operationManager, IOperationTaskManager operationTaskManager) : base(bus, workflowOptions, logger, operationManager, operationTaskManager)
    {
    }

    protected override ValueTask<(IOperationTask, object)> CreateTask(Guid operationId, 
        Guid initiatingTaskId, object command,
        DateTimeOffset created, object? additionalData,
        IDictionary<string, string>? additionalHeaders)
    {
        if (additionalData != null)
        {
            var additionalDataValid = false;
            if (additionalData is Resource resource && command is IGenericResourceCommand resourceCommand)
            {
                resourceCommand.Resource = resource;
                additionalDataValid = true;
            }

            if (additionalData is Resource[] resources && command is IGenericResourcesCommand resourcesCommand)
            {
                resourcesCommand.Resources = resources;
                additionalDataValid = true;
            }

            if (!additionalDataValid)
            {
                throw new InvalidOperationException(
                    $"{nameof(CreateTask)}: Invalid {nameof(additionalData)} {additionalData.GetType()} passed for command {command.GetType()}");
            }
        }

        return base.CreateTask(operationId, initiatingTaskId, command,created, additionalData, additionalHeaders);
    }
}