using System;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Seeding;

internal class ProjectSeeder(
    ChangeTrackingConfig config,
    IFileSystem fileSystem,
    ILogger logger,
    IStateStore stateStore)
    : SeederBase(fileSystem, config.ProjectsConfigPath)
{
    protected override async Task SeedAsync(
        Guid entityId,
        string json,
        CancellationToken cancellationToken = default)
    {
        var tenantId = EryphConstants.DefaultTenantId;

        var project = await stateStore.For<Project>().GetBySpecAsync(
            new ProjectSpecs.GetById(tenantId, entityId),
            cancellationToken);

        if (project is not null)
            return;

        var projectConfig = JsonSerializer.Deserialize<ProjectConfigModel>(json);
        if (projectConfig is null)
        {
            logger.LogWarning("Could not seed project {entityId} because the config is invalid", entityId);
            return;
        }

        logger.LogInformation("Project '{entityId}' not found in state db. Creating project record.", entityId);

        project = new Project
        {
            Id = entityId,
            Name = projectConfig.Name ?? throw new InvalidOperationException("Project name cannot be null"),
            TenantId = tenantId,
            ProjectRoles = projectConfig.Assignments?.Map(ac => new ProjectRoleAssignment
            {
                IdentityId = ac.IdentityId ?? throw new InvalidOperationException("Identity ID cannot be null"),
                RoleId = RoleNames.GetRoleId(ac.RoleName ?? throw new InvalidOperationException("Role name cannot be null"))
                    .ToEither(Error.New($"The role {ac.RoleName} does not exist"))
                    .IfLeft(e => e.ToException().Rethrow<Guid>()),
            }).ToList() ?? [],
        };

        await stateStore.For<Project>().AddAsync(project, cancellationToken);
        await stateStore.SaveChangesAsync(cancellationToken);
    }
}
