using System;
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
    internal class AddProjectMemberCommandHandler : IHandleMessages<OperationTask<AddProjectMemberCommand>>
    {
        private readonly IStateStore _stateStore;
        private readonly ITaskMessaging _messaging;
        
        public AddProjectMemberCommandHandler(IStateStore stateStore, 
            ITaskMessaging messaging)
        {
            _stateStore = stateStore;
            _messaging = messaging;
        }

        public async Task Handle(OperationTask<AddProjectMemberCommand> message)
        {
            var stoppingToken = new CancellationTokenSource(10000);

            var existingProject = await _stateStore.For<Project>().GetBySpecAsync(
                new ProjectSpecs.GetById(message.Command.TenantId,
                    message.Command.ProjectId), stoppingToken.Token);

            var assignmentRepo = _stateStore.For<ProjectRoleAssignment>();
            if (existingProject == null)
            {
                await _messaging.FailTask(message,
                    $"Project with id '{message.Command.ProjectId}' not found in tenant");
                return;
            }

            var roleId = Guid.Empty;
            if (message.Command.RoleId == EryphConstants.BuildInRoles.Owner ||
                message.Command.RoleId == EryphConstants.BuildInRoles.Contributor ||
                message.Command.RoleId == EryphConstants.BuildInRoles.Reader)
                roleId = message.Command.RoleId;

            if (roleId == Guid.Empty)
            {
                await _messaging.FailTask(message,
                    $"Role with id '{message.Command.RoleId}' not found.");
                return;
            }

            var roleAssignment = new ProjectRoleAssignment()
            {
                Id = message.Command.CorrelationId,
                IdentityId = message.Command.MemberId,
                ProjectId = existingProject.Id,
                RoleId = roleId
            };

            var existingAssignment = await assignmentRepo.GetBySpecAsync(
                               new ProjectRoleAssignmentSpecs.GetByMemberAndRole(
                              roleAssignment.ProjectId,
                              roleAssignment.IdentityId,
                              roleAssignment.RoleId), stoppingToken.Token);

            if (existingAssignment != null)
            {
                await _messaging.FailTask(message,
                    $"Role with id '{message.Command.RoleId}' is already assigned to member '{roleAssignment.IdentityId}'.");
                return;
            }

            await assignmentRepo.AddAsync(
                roleAssignment, stoppingToken.Token);
        

        await _messaging.CompleteTask(message, 
            new ProjectMemberReference
            {
                ProjectId = message.Command.CorrelationId,
                ProjectName = existingProject.Name,
                AssignmentId = roleAssignment.Id
            });
        }
    }
}
