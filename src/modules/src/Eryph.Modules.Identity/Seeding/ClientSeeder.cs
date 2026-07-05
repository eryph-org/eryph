using System;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.Modules.Identity.ChangeTracking;
using Eryph.Modules.Identity.Services;

namespace Eryph.Modules.Identity.Seeding;

/// <summary>
/// Rebuilds client applications from the on-disk config mirror on startup (replacing the eryph-zero
/// <c>IdentityClientSeeder</c>). Reads the <see cref="ClientConfigModel"/> files and adds any that are
/// not already present. Secrets in the files are already hashed, so they are added with
/// <c>hashedSecret: true</c>. An already-present row is left untouched. The <c>system-client</c> is not
/// seeded here at all — it is owned end to end by <see cref="Bootstrap.SystemClientBootstrap"/> (its
/// private key on disk and its database row reconciled directly), so the seeder skips it.
/// </summary>
internal class ClientSeeder(
    IdentityChangeTrackingConfig config,
    IFileSystem fileSystem,
    IClientService clientService)
    : IConfigSeeder<IdentityModule>
{
    public async Task Execute(CancellationToken stoppingToken)
    {
        if (!config.SeedDatabase)
            return;

        if (string.IsNullOrEmpty(config.ClientsConfigPath)
            || !fileSystem.Directory.Exists(config.ClientsConfigPath))
            return;

        foreach (var file in fileSystem.Directory.EnumerateFiles(config.ClientsConfigPath, "*.json"))
            try
            {
                var content = await fileSystem.File.ReadAllTextAsync(file, Encoding.UTF8, stoppingToken);
                var model = JsonSerializer.Deserialize<ClientConfigModel>(content);
                if (model is null || string.IsNullOrEmpty(model.ClientId))
                    continue;

                // The system client is owned by SystemClientBootstrap, which materialises and reconciles
                // it directly against the on-disk private key. Skipping it here avoids seeding a stale
                // mirror over the bootstrap's row (the mirror is only a change-tracking export byproduct).
                if (model.ClientId == EryphConstants.SystemClientId)
                    continue;

                var existing = await clientService.Get(model.ClientId, model.TenantId, stoppingToken);
                if (existing is not null)
                    continue;

                await clientService.Add(model.ToDescriptor(), true, stoppingToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to seed identity client from file '{file}'.", ex);
            }
    }
}
