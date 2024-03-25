using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Modules.Controller;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Runtime.Zero.Configuration.Projects
{
    [UsedImplicitly]
    internal class ProjectSeeder : IConfigSeeder<ControllerModule>
    {
        private readonly ILogger _logger;
        private readonly IStateStore _stateStore;
        private readonly IConfigReader<ProjectConfigModel> _configReader;
        private readonly IMapper _mapper;

        public ProjectSeeder(
            ILogger logger,
            IStateStore stateStore,
            IConfigReader<ProjectConfigModel> configReader,
            IMapper mapper)
        {
            _logger = logger;
            _stateStore = stateStore;
            _configReader = configReader;
            _mapper = mapper;
        }

        public async Task Execute(CancellationToken stoppingToken)
        {
            _logger.LogDebug("Entering state db project seeder");
            
            await EnsureDefaultTenant(stoppingToken);
            //await EnsureProjectsFromConfig(stoppingToken);
            await EnsureDefaultProject(stoppingToken);
        }

        private async Task EnsureDefaultTenant(
            CancellationToken stoppingToken)
        {
            var tenantId = EryphConstants.DefaultTenantId;
            var tenant = await _stateStore.For<Tenant>().GetByIdAsync(tenantId, stoppingToken);

            if (tenant is null)
            {
                _logger.LogInformation("Default tenant '{tenantId}' not found in state db. Creating tenant record.", tenantId);

                tenant = new Tenant { Id = tenantId };
                await _stateStore.For<Tenant>().AddAsync(tenant, stoppingToken);
                await _stateStore.For<Tenant>().SaveChangesAsync(stoppingToken);
            }
        }

        private async Task EnsureProjectsFromConfig(
            CancellationToken stoppingToken)
        {
            await foreach (var projectConfig in _configReader.ReadAsync(stoppingToken))
            {
                await EnsureProjectFromConfig(projectConfig, stoppingToken);
            }
        }

        private async Task EnsureProjectFromConfig(
            ProjectConfigModel projectConfig,
            CancellationToken stoppingToken)
        {
            var tenantId = EryphConstants.DefaultTenantId;
            var project = await _stateStore.For<Project>().GetBySpecAsync(
                new ProjectSpecs.GetById(tenantId, projectConfig.Id),
                stoppingToken);

            // TODO Do we need to reseed the assignments when the project already exists?
            if (project is null)
            {
                _logger.LogInformation("Project '{projectId}' not found in state db. Creating project record.", projectConfig.Id);

                project = _mapper.Map<Project>(projectConfig);

                await _stateStore.For<Project>().AddAsync(project, stoppingToken);
                await _stateStore.For<Project>().SaveChangesAsync(stoppingToken);

                var assignmentConfigs = projectConfig.Assignments ?? Array.Empty<ProjectRoleAssignmentConfigModel>();
                foreach (var assignmentConfig in assignmentConfigs)
                {
                    var assignment = _mapper.Map<ProjectRoleAssignment>(assignmentConfig);
                    assignment.ProjectId = project.Id;
                    await _stateStore.For<ProjectRoleAssignment>().AddAsync(assignment, stoppingToken);
                    await _stateStore.For<ProjectRoleAssignment>().SaveChangesAsync(stoppingToken);
                }
            }
        }

        private async Task EnsureDefaultProject(CancellationToken stoppingToken)
        {
            var tenantId = EryphConstants.DefaultTenantId;
            var projectId = EryphConstants.DefaultProjectId;
            var project = await _stateStore.For<Project>().GetBySpecAsync(
                new ProjectSpecs.GetById(tenantId, projectId),
                stoppingToken);

            if (project is null)
            {
                _logger.LogInformation("Default project '{projectId}' not found in state db. Creating project record.", projectId);

                project = new Project
                {
                    Id = projectId,
                    Name = "default",
                    TenantId = tenantId
                };
                await _stateStore.For<Project>().AddAsync(project, stoppingToken);
                await _stateStore.For<Project>().SaveChangesAsync(stoppingToken);

                // TODO Should we create the assignment for the system-client? If not,
                // we should also not create it when creating project in the API.
                var assignment = new ProjectRoleAssignment
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    IdentityId = "system-client",
                    RoleId = EryphConstants.BuildInRoles.Owner
                };

                await _stateStore.For<ProjectRoleAssignment>().AddAsync(assignment, stoppingToken);
                await _stateStore.For<ProjectRoleAssignment>().SaveChangesAsync(stoppingToken);
            }
        }
    }
}
