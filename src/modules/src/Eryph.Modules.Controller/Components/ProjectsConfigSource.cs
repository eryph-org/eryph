using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Builds the <see cref="ConfigDomain.Projects"/> payload from the state database
/// (the pilot domain — projects are already DB-authoritative).
/// </summary>
internal sealed class ProjectsConfigSource(
    IStateStoreRepository<Project> projects)
    : IConfigSource
{
    public ConfigDomain Domain => ConfigDomain.Projects;

    public async Task<string> BuildPayloadAsync(CancellationToken cancellationToken)
    {
        var all = await projects.ListAsync(cancellationToken);
        var payload = all
            .Where(p => !p.BeingDeleted)
            .Select(p => new ProjectConfigEntry(p.Id, p.Name, p.TenantId))
            .ToArray();
        return JsonSerializer.Serialize(payload);
    }

    private sealed record ProjectConfigEntry(System.Guid Id, string Name, System.Guid TenantId);
}
