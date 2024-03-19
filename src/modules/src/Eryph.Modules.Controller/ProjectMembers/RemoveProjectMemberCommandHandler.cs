using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages;
using Eryph.Messages.Projects;
using Eryph.Modules.Controller.DataServices;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using Rebus.Handlers;

namespace Eryph.Modules.Controller.ProjectMembers
{
    [UsedImplicitly]
    internal class RemoveProjectMemberCommandHandler : IHandleMessages<OperationTask<RemoveProjectMemberCommand>>
    {
        private readonly IStateStore _stateStore;
        private readonly IDataUpdateService<ProjectRoleAssignment> _assignmentUpdateService;
        private readonly ITaskMessaging _messaging;
        
        public RemoveProjectMemberCommandHandler(
            IStateStore stateStore,
            IDataUpdateService<ProjectRoleAssignment> assignmentUpdateService,
            ITaskMessaging messaging)
        {
            _stateStore = stateStore;
            _assignmentUpdateService = assignmentUpdateService;
            _messaging = messaging;
        }

        public async Task Handle(OperationTask<RemoveProjectMemberCommand> message)
        {
            var stoppingToken = new CancellationTokenSource(10000);

            var assignment = await _stateStore.Read<ProjectRoleAssignment>()
                .GetBySpecAsync(
                    new ProjectRoleAssignmentSpecs.GetById(
                        message.Command.AssignmentId, message.Command.ProjectId),
                    stoppingToken.Token);

            if (assignment != null)
            {
                if (assignment.RoleId == EryphConstants.BuildInRoles.Owner && 
                    message.Command.CurrentIdentityId == assignment.IdentityId)
                {
                    await _messaging.FailTask(message,
                        "You cannot remove your own owner role from the project.");
                    return;
                }

                await _assignmentUpdateService.DeleteAsync(assignment, stoppingToken.Token);
            }

            await _messaging.CompleteTask(message,
                    new ProjectMemberReference
                    {
                        ProjectId = message.Command.CorrelationId,
                        ProjectName = assignment?.Project.Name,
                        AssignmentId = message.Command.AssignmentId,
                    });
        }
    }
}
