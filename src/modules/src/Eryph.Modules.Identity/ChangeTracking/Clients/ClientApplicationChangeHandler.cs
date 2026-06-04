using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.ChangeTracking;
using Eryph.Modules.Identity.Services;

namespace Eryph.Modules.Identity.ChangeTracking.Clients;

/// <summary>
/// Exports a client application to the on-disk config mirror (the same directory and
/// <see cref="Eryph.Configuration.Model.ClientConfigModel"/> format the seeder reads), or removes the
/// file when the client is gone. Supersedes the eryph-zero <c>ClientServiceWithConfigServiceDecorator</c>
/// write-through; the system client's bootstrap file is still written by the (cross-module) system client
/// generator and merely re-exported here once it is in the database.
/// </summary>
internal class ClientApplicationChangeHandler(
    IdentityChangeTrackingConfig config,
    IFileSystem fileSystem,
    IClientService clientService)
    : IChangeHandler<ClientApplicationChange>
{
    public async Task HandleChangeAsync(
        ClientApplicationChange change,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(
            config.ClientsConfigPath, $"{IdentityConfigFileName.Coerce(change.ClientId)}.json");

        var descriptor = await clientService.Get(change.ClientId, change.TenantId, cancellationToken);
        if (descriptor is null)
        {
            if (fileSystem.File.Exists(path))
                fileSystem.File.Delete(path);
            return;
        }

        var model = descriptor.FromDescriptor();
        fileSystem.Directory.CreateDirectory(config.ClientsConfigPath);
        var json = JsonSerializer.Serialize(model);
        // Write atomically: a crash mid-write must not leave a truncated mirror file that would fail the
        // seeder on the next start. Write to a temp file (the seeder only reads "*.json", so it ignores
        // "*.json.tmp"), then atomically replace the target.
        var tempPath = path + ".tmp";
        await fileSystem.File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, cancellationToken);
        fileSystem.File.Move(tempPath, path, overwrite: true);
    }
}
