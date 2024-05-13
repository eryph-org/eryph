using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
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

internal class ProjectSeeder : SeederBase
{
    private readonly ILogger _logger;
    private readonly IStateStore _stateStore;

    public ProjectSeeder(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        ILogger logger,
        IStateStore stateStore)
        : base(fileSystem, config.ProjectsConfigPath)
    {
        _logger = logger;
        _stateStore = stateStore;
    }

    protected override async Task SeedAsync(
        Guid entityId,
        string json,
        CancellationToken cancellationToken = default)
    {
        var tenantId = EryphConstants.DefaultTenantId;

        var project = await _stateStore.For<Project>().GetBySpecAsync(
            new ProjectSpecs.GetById(tenantId, entityId),
            cancellationToken);

        if (project is not null)
            return;

        var projectConfig = JsonSerializer.Deserialize<ProjectConfigModel>(json);
        if (projectConfig is null)
        {
            _logger.LogWarning("Could not seed project {entityId} because the config is invalid", entityId);
            return;
        }

        _logger.LogInformation("Project '{entityId}' not found in state db. Creating project record.", entityId);

        project = new Project()
        {
            Id = entityId,
            Name = projectConfig.Name,
            TenantId = tenantId,
            ProjectRoles = projectConfig.Assignments?.Map(ac => new ProjectRoleAssignment()
            {
                IdentityId = ac.IdentityId,
                RoleId = RoleNames.GetRoleId(ac.RoleName)
                    .ToEither(Error.New($"The role {ac.RoleName} does not exist"))
                    .IfLeft(e => e.ToException().Rethrow<Guid>())
            }).ToList() ?? [],
        };

        await _stateStore.For<Project>().AddAsync(project, cancellationToken);
        await _stateStore.SaveChangesAsync(cancellationToken);
    }
}