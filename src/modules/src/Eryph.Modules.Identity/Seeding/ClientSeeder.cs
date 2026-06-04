using System;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Modules.Identity.ChangeTracking;
using Eryph.Modules.Identity.Services;

namespace Eryph.Modules.Identity.Seeding;

/// <summary>
/// Rebuilds client applications from the on-disk config mirror on startup (replacing the eryph-zero
/// <c>IdentityClientSeeder</c>). Reads the <see cref="ClientConfigModel"/> files — including the
/// system client's bootstrap file written by the system client generator — and adds any that are not
/// already present. Secrets in the files are already hashed, so they are added with
/// <c>hashedSecret: true</c>.
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
        {
            try
            {
                var content = await fileSystem.File.ReadAllTextAsync(file, Encoding.UTF8, stoppingToken);
                var model = JsonSerializer.Deserialize<ClientConfigModel>(content);
                if (model is null || string.IsNullOrEmpty(model.ClientId))
                    continue;

                var existing = await clientService.Get(model.ClientId, model.TenantId, stoppingToken);
                if (existing is not null)
                    continue;

                await clientService.Add(model.ToDescriptor(), hashedSecret: true, stoppingToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to seed identity client from file '{file}'.", ex);
            }
        }
    }
}
