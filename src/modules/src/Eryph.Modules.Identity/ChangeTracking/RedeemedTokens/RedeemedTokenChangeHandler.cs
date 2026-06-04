using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.ModuleCore.ChangeTracking;

namespace Eryph.Modules.Identity.ChangeTracking.RedeemedTokens;

/// <summary>
/// Exports a redeemed enrollment-token record to the on-disk config mirror, or removes the file when the
/// record is gone (e.g. pruned). The file name is the <c>jti</c> (a GUID, so it is a safe file name); the
/// authoritative value is the <c>Jti</c> inside the file, which the seeder uses to rebuild the row.
/// </summary>
internal class RedeemedTokenChangeHandler(
    IdentityChangeTrackingConfig config,
    IFileSystem fileSystem,
    IIdentityDbRepository<RedeemedEnrollmentToken> repository)
    : IChangeHandler<RedeemedTokenChange>
{
    public async Task HandleChangeAsync(
        RedeemedTokenChange change,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(config.RedeemedTokensConfigPath, $"{change.Jti}.json");

        var entity = await repository.GetByIdAsync(change.Jti, cancellationToken);
        if (entity is null)
        {
            if (fileSystem.File.Exists(path))
                fileSystem.File.Delete(path);
            return;
        }

        var model = new RedeemedTokenConfigModel
        {
            Jti = entity.Jti,
            ExpiresAt = entity.ExpiresAt,
        };

        fileSystem.Directory.CreateDirectory(config.RedeemedTokensConfigPath);
        var json = JsonSerializer.Serialize(model);
        await fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
