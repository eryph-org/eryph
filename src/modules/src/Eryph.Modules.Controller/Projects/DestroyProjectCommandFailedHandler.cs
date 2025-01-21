using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Events;
using Eryph.Messages.Projects;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Projects;

internal class DestroyProjectCommandFailedHandler(
    ILogger logger,
    WorkflowOptions workflowOptions)
    : IHandleMessages<OperationTaskStatusEvent<DestroyProjectCommand>>
{
    public async Task Handle(OperationTaskStatusEvent<DestroyProjectCommand> message)
    {
        if (!message.OperationFailed)
            return;

        if (message.GetMessage(workflowOptions.JsonSerializerOptions) is not DestroyProjectCommand command)
            return;

        logger.LogError("Failed to destroy project {ProjectId}", command.ProjectId);
    }
}