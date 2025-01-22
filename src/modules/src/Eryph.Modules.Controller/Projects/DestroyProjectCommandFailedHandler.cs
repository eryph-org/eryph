using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Events;
using Eryph.Messages.Projects;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.Projects;

/// <summary>
/// The handler removes the <see cref="Project.BeingDeleted"/> flag
/// in case the <see cref="DestroyProjectSaga"/> fails.
/// </summary>
/// <remarks>
/// The project is in a partially deleted state in case the saga fails.
/// Most likely, the project is not usable anymore. We remove the flag
/// just in case.
/// </remarks>
internal class DestroyProjectCommandFailedHandler(
    ILogger logger,
    IStateStore stateStore,
    WorkflowOptions workflowOptions)
    : IHandleMessages<OperationTaskStatusEvent<DestroyProjectCommand>>
{
    public async Task Handle(OperationTaskStatusEvent<DestroyProjectCommand> message)
    {
        if (!message.OperationFailed)
            return;

        if (message.GetMessage(workflowOptions.JsonSerializerOptions) is not DestroyProjectCommand command)
            return;

        var project = await stateStore.For<Project>().GetByIdAsync(command.ProjectId);
        if (project is null)
            return;

        logger.LogInformation("Could not delete the project {ProjectId}. Removing the BeingDeleted flag.",
            command.ProjectId);

        project.BeingDeleted = false;
        await stateStore.SaveChangesAsync();
    }
}
