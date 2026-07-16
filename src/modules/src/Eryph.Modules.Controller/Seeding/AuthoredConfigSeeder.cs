using System;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Seeding;

/// <summary>
/// Restores the operator-authored configuration, and the sites it declares, from the mirror.
/// </summary>
/// <remarks>
/// This must run before any seeder which recreates resources: realizing a project's networks
/// resolves the site of their environment, which is only knowable from the authored environment
/// catalog. Without it a deployment with environment-scoped networks could not be seeded at all
/// after the state database is re-created.
/// </remarks>
[UsedImplicitly]
internal class AuthoredConfigSeeder(
    ChangeTrackingConfig config,
    IFileSystem fileSystem,
    ISitesConfigRealizer sitesConfigRealizer,
    IStateStore stateStore,
    ILogger logger)
    : IConfigSeeder<ControllerModule>
{
    public async Task Execute(CancellationToken stoppingToken)
    {
        var path = Path.Combine(config.AuthoredConfigsPath, "authored.json");
        if (!fileSystem.File.Exists(path))
            return;

        // The database is only seeded when it is empty, so anything already authored means this is
        // not a restore and the mirror must not overwrite it.
        var existing = await stateStore.For<AuthoredConfig>().ListAsync(stoppingToken);
        if (existing.Count > 0)
            return;

        AuthoredConfigsConfigModel? mirror;
        try
        {
            fileSystem.File.Copy(path, $"{path}.bak", true);
            var content = await fileSystem.File.ReadAllTextAsync(path, Encoding.UTF8, stoppingToken);
            mirror = JsonSerializer.Deserialize<AuthoredConfigsConfigModel>(content);
        }
        catch (Exception ex)
        {
            throw new SeederException($"Failed to seed database from file '{path}'", ex);
        }

        foreach (var authored in mirror?.AuthoredConfigs ?? [])
        {
            if (!Enum.TryParse<ConfigDomain>(authored.Domain, out var domain))
            {
                // A domain this build does not know: an older mirror, or one written by a newer
                // version. Restoring it is impossible and dropping it silently would hide that the
                // operator's configuration is incomplete.
                logger.LogWarning(
                    "Skipping the mirrored '{Domain}' configuration: this is not a known "
                    + "configuration domain.", authored.Domain);
                continue;
            }

            await stateStore.For<AuthoredConfig>().AddAsync(
                new AuthoredConfig
                {
                    Id = Guid.NewGuid(),
                    Domain = domain,
                    Scope = authored.Scope ?? "",
                    Version = authored.Version,
                    Payload = authored.Payload ?? "",
                    CreatedBy = authored.CreatedBy,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                stoppingToken);

            logger.LogInformation(
                "Restored the authored {Domain} configuration (version {Version}) from the mirror.",
                domain, authored.Version);
        }

        // Nothing is saved until the sites are realized as well: the restored rows and the sites they
        // declare go in together, in the single SaveChanges at the end of RealizeSites. Committing the
        // rows first would be one-way — this seeder only runs while nothing is authored, so a failure
        // in RealizeSites would leave a restored catalog whose sites nothing ever creates, and the
        // early return above would skip the retry on every later start.
        await RealizeSites(mirror, stoppingToken);
    }

    private async Task RealizeSites(AuthoredConfigsConfigModel? mirror, CancellationToken stoppingToken)
    {
        // The sites are records, so they were dropped with the database; they are declared by the
        // environments configuration and have to exist before anything is pinned to them.
        foreach (var authored in mirror?.AuthoredConfigs ?? [])
        {
            if (authored.Domain != nameof(ConfigDomain.Environments) || authored.Payload is null)
                continue;

            EnvironmentsConfig environments;
            try
            {
                environments = EnvironmentsConfigYamlSerializer.Deserialize(authored.Payload);
            }
            catch (Exception ex)
            {
                // A mirrored payload which cannot be read is a broken restore, not something to
                // start up around: the sites it declares would be missing and every resource pinned
                // to them unusable. Fail like the rest of the seeding does.
                throw new SeederException(
                    "Failed to seed the sites from the mirrored environment configuration", ex);
            }

            await sitesConfigRealizer.RealizeSites(environments, stoppingToken);
        }

        await stateStore.SaveChangesAsync(stoppingToken);
    }
}
