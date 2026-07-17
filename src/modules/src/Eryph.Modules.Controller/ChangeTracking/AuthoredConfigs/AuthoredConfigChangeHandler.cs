using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.StateDb;
using Eryph.StateDb.Model;

namespace Eryph.Modules.Controller.ChangeTracking.AuthoredConfigs;

/// <summary>
/// Mirrors the current authored value of every configuration domain to disk, so it survives the
/// re-creation of the state database.
/// </summary>
internal class AuthoredConfigChangeHandler(
    ChangeTrackingConfig config,
    IFileSystem fileSystem,
    IStateStore stateStore)
    : IChangeHandler<AuthoredConfigChange>
{
    public async Task HandleChangeAsync(
        AuthoredConfigChange change,
        CancellationToken cancellationToken = default)
    {
        var authoredConfigs = await stateStore.For<AuthoredConfig>().ListAsync(cancellationToken);

        // The current value is the highest version per domain and scope. Only that is mirrored, not
        // the version history: the history is an audit of who changed what, whereas what has to
        // survive is the configuration itself.
        var current = authoredConfigs
            .GroupBy(c => (c.Domain, c.Scope))
            .Select(g => g.OrderByDescending(c => c.Version).First())
            .Select(c => new AuthoredConfigConfigModel
            {
                Domain = c.Domain.ToString(),
                Scope = c.Scope,
                Version = c.Version,
                Payload = c.Payload,
                CreatedBy = c.CreatedBy,
            })
            .OrderBy(c => c.Domain)
            .ThenBy(c => c.Scope)
            .ToArray();

        var path = Path.Combine(config.AuthoredConfigsPath, "authored.json");
        await fileSystem.File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(new AuthoredConfigsConfigModel { AuthoredConfigs = current }),
            Encoding.UTF8,
            cancellationToken);
    }
}
