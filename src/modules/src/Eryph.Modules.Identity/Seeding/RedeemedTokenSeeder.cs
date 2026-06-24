using System;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.Modules.Identity.ChangeTracking;
using Eryph.Modules.Identity.ChangeTracking.RedeemedTokens;

namespace Eryph.Modules.Identity.Seeding;

/// <summary>
/// Rebuilds the redeemed enrollment-token records from the on-disk config mirror on startup, so the
/// single-use guarantee survives a database that was dropped and recreated (eryph-zero) or migrated.
/// </summary>
internal class RedeemedTokenSeeder(
    IdentityChangeTrackingConfig config,
    IFileSystem fileSystem,
    IIdentityDbRepository<RedeemedEnrollmentToken> repository)
    : IConfigSeeder<IdentityModule>
{
    public async Task Execute(CancellationToken stoppingToken)
    {
        if (!config.SeedDatabase)
            return;

        if (!fileSystem.Directory.Exists(config.RedeemedTokensConfigPath))
            return;

        var added = false;
        foreach (var file in fileSystem.Directory.EnumerateFiles(config.RedeemedTokensConfigPath, "*.json"))
            try
            {
                var content = await fileSystem.File.ReadAllTextAsync(file, Encoding.UTF8, stoppingToken);
                var model = JsonSerializer.Deserialize<RedeemedTokenConfigModel>(content);
                if (model is null || string.IsNullOrEmpty(model.Jti))
                    continue;

                var existing = await repository.GetByIdAsync(model.Jti, stoppingToken);
                if (existing is not null)
                    continue;

                await repository.AddAsync(
                    new RedeemedEnrollmentToken { Jti = model.Jti, ExpiresAt = model.ExpiresAt },
                    stoppingToken);
                added = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to seed redeemed enrollment token from file '{file}'.", ex);
            }

        if (added)
            await repository.SaveChangesAsync(stoppingToken);
    }
}
