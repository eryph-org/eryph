using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages;
using Eryph.Messages.Projects;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;
using Eryph.ConfigModel.Networks;

namespace Eryph.Modules.Controller.Projects
{
    [UsedImplicitly]
    internal class CreateProjectCommandHandler : IHandleMessages<OperationTask<CreateProjectCommand>>
    {
        private readonly IStateStore _stateStore;
        private readonly ITaskMessaging _messaging;
        private readonly IDefaultNetworkConfigRealizer _defaultNetworkConfigRealizer;
        
        public CreateProjectCommandHandler(
            IStateStore stateStore,
            INetworkProviderManager networkProviderManager, 
            ITaskMessaging messaging,
            IDefaultNetworkConfigRealizer defaultNetworkConfigRealizer,
            INetworkConfigRealizer networkConfigRealizer)
        {
            _stateStore = stateStore;
            _messaging = messaging;
            _defaultNetworkConfigRealizer = defaultNetworkConfigRealizer;
        }

        public async Task Handle(OperationTask<CreateProjectCommand> message)
        {
            var stoppingToken = new CancellationTokenSource(10000);

            var name = message.Command.ProjectName;
            var validation = ProjectName.NewValidation(name);
            if (validation.IsFail)
            {
                await _messaging.FailTask(message, validation);
                return;
            }

            if (name == "default")
            {
                await _messaging.FailTask(message, $"The project name '{name}' is reserved.");
                return;
            }

            var existingProject = await _stateStore.For<Project>().GetBySpecAsync(
                new ProjectSpecs.GetByName(message.Command.TenantId, name), stoppingToken.Token);

            if (existingProject != null)
            {
                await _messaging.FailTask(message,
                    $"Project with name '{name}' already exists in tenant. Project names have to be unique within a tenant.");
                return;
            }

            var project = await _stateStore.For<Project>().AddAsync(
                new Project
                {
                    Id = message.Command.CorrelationId, Name = name,
                    TenantId = message.Command.TenantId
                }, stoppingToken.Token);

            await _messaging.ProgressMessage(message, $"Creating project '{name}'");

            if (!string.IsNullOrWhiteSpace(message.Command.IdentityId))
            {
                var roleAssignment = new ProjectRoleAssignment()
                {
                    Id = Guid.NewGuid(),
                    IdentityId = message.Command.IdentityId,
                    ProjectId = project.Id,
                    RoleId = EryphConstants.BuildInRoles.Owner
                };

                await _stateStore.For<ProjectRoleAssignment>().AddAsync(
                    roleAssignment, stoppingToken.Token);
            }

            if (!message.Command.NoDefaultNetwork)
            {
                await _messaging.ProgressMessage(message, $"Creating default network for project '{name}'.");
                await _defaultNetworkConfigRealizer.RealizeDefaultConfig(project.Id);
            }

            await _messaging.CompleteTask(message, new ProjectReference
            {
                ProjectId = message.Command.CorrelationId,
                ProjectName = name
            });
        }
    }
}
