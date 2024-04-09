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

namespace Eryph.ZeroState.Projects;

internal class ZeroStateProjectChangeHandler : IZeroStateChangeHandler<ZeroStateProjectChange>
{
    private readonly IZeroStateConfig _config;
    private readonly IFileSystem _fileSystem;
    private readonly IStateStore _stateStore;

    public ZeroStateProjectChangeHandler(
        IZeroStateConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore)
    {
        _config = config;
        _fileSystem = fileSystem;
        _stateStore = stateStore;
    }

    public async Task HandleChangeAsync(
        ZeroStateProjectChange change,
        CancellationToken cancellationToken = default)
    {
        foreach (var projectId in change.ProjectIds)
        {
            await HandleChangeAsync(projectId, cancellationToken);
        }
    }

    private async Task HandleChangeAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_config.ProjectsConfigPath, $"{projectId}.json");
        var project = await _stateStore.For<Project>().GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            _fileSystem.File.Delete(path);
            _fileSystem.File.Delete(Path.Combine(_config.ProjectNetworksConfigPath, $"{projectId}.json"));
            _fileSystem.File.Delete(Path.Combine(_config.ProjectNetworkPortsConfigPath, $"{projectId}.json"));
            return;
        }

        await _stateStore.LoadCollectionAsync(project, p => p.ProjectRoles, cancellationToken);
        var projectConfig = new ProjectConfigModel()
        {
            TenantId = project.TenantId,
            Name = project.Name,
            Assignments = project.ProjectRoles.Map(r => new ProjectRoleAssignmentConfigModel()
            {
                IdentityId = r.IdentityId,
                RoleName = RoleNames.GetRoleName(r.RoleId),
            }).ToArray(),
        };

        var json = JsonSerializer.Serialize(projectConfig);
        await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
