using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages;
using Eryph.Messages.Projects;
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
        private readonly ITaskMessaging _messaging;
        
        public RemoveProjectMemberCommandHandler(IStateStore stateStore, 
            ITaskMessaging messaging)
        {
            _stateStore = stateStore;
            _messaging = messaging;
        }

        public async Task Handle(OperationTask<RemoveProjectMemberCommand> message)
        {
            var stoppingToken = new CancellationTokenSource(10000);

            var assignment = await _stateStore.For<ProjectRoleAssignment>()
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


                await _stateStore.For<ProjectRoleAssignment>().DeleteAsync(assignment, stoppingToken.Token);
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
