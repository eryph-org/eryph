using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.Extensions.Logging;

namespace Eryph.ZeroState
{
    internal class ZeroStateProjectSeeder : ZeroStateProjectSeederBase
    {
        private readonly ILogger _logger;
        private readonly IStateStore _stateStore;

        public ZeroStateProjectSeeder(
            IFileSystem fileSystem,
            IZeroStateConfig config,
            ILogger logger,
            IStateStore stateStore)
            : base(fileSystem, config.ProjectsConfigPath, logger)
        {
            _logger = logger;
            _stateStore = stateStore;
        }

        protected override async Task SeedProjectAsync(
            Guid projectId,
            string json,
            CancellationToken cancellationToken = default)
        {
            var tenantId = EryphConstants.DefaultTenantId;
            var tenant = await _stateStore.For<Tenant>().GetByIdAsync(tenantId, cancellationToken);

            // TODO move to proper place
            if (tenant is null)
            {
                _logger.LogInformation("Default tenant '{tenantId}' not found in state db. Creating tenant record.", tenantId);

                tenant = new Tenant { Id = tenantId };
                await _stateStore.For<Tenant>().AddAsync(tenant, cancellationToken);
            }

            var project = await _stateStore.For<Project>().GetBySpecAsync(
                new ProjectSpecs.GetById(tenantId, projectId),
                cancellationToken);

            // TODO Do we need to reseed the assignments when the project already exists?
            if (project is not null)
                return;

            _logger.LogInformation("Project '{projectId}' not found in state db. Creating project record.", projectId);

            var projectConfig = JsonSerializer.Deserialize<ProjectConfigModel>(json);

            project = new Project()
            {
                Name = projectConfig.Name,
                TenantId = tenantId,
                ProjectRoles = projectConfig.Assignments?.Map(ac => new ProjectRoleAssignment()
                {
                    IdentityId = ac.IdentityId,
                    RoleId = ac.RoleId,
                }).ToList(),
            };

            await _stateStore.For<Project>().AddAsync(project, cancellationToken);
            await _stateStore.SaveChangesAsync(cancellationToken);
        }
    }
}
