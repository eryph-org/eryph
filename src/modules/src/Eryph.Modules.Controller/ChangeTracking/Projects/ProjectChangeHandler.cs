using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.ChangeTracking.Projects;

internal class ProjectChangeHandler(
    ChangeTrackingConfig config,
    IFileSystem fileSystem,
    IStateStore stateStore)
    : IChangeHandler<ProjectChange>
{
    public async Task HandleChangeAsync(
        ProjectChange change,
        CancellationToken cancellationToken = default)
    {
        var projectId = change.ProjectId;
        var path = Path.Combine(config.ProjectsConfigPath, $"{projectId}.json");
        var project = await stateStore.For<Project>().GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            fileSystem.File.Delete(path);
            fileSystem.File.Delete(Path.Combine(config.ProjectNetworksConfigPath, $"{projectId}.json"));
            fileSystem.File.Delete(Path.Combine(config.ProjectNetworkPortsConfigPath, $"{projectId}.json"));
            return;
        }

        await stateStore.LoadCollectionAsync(project, p => p.ProjectRoles, cancellationToken);
        var projectConfig = new ProjectConfigModel
        {
            TenantId = project.TenantId,
            Name = project.Name,
            Assignments = project.ProjectRoles.Map(r => new ProjectRoleAssignmentConfigModel
            {
                IdentityId = r.IdentityId,
                RoleName = RoleNames.GetRoleName(r.RoleId),
            }).ToArray(),
        };

        var json = JsonSerializer.Serialize(projectConfig);
        await fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
