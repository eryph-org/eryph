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
/// record is gone (e.g. pruned). The file name is the (coerced) <c>jti</c>; the authoritative value is
/// the <c>Jti</c> inside the file, which the seeder uses to rebuild the row.
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
        var path = Path.Combine(
            config.RedeemedTokensConfigPath, $"{IdentityConfigFileName.Coerce(change.Jti)}.json");

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
        // Write atomically: a crash mid-write must not leave a truncated mirror file that would fail the
        // seeder on the next start. Write to a temp file (the seeder only reads "*.json", so it ignores
        // "*.json.tmp"), then atomically replace the target.
        var tempPath = path + ".tmp";
        await fileSystem.File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, cancellationToken);
        fileSystem.File.Move(tempPath, path, overwrite: true);
    }
}
