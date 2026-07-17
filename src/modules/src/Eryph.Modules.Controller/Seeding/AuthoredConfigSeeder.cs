using System;
using System.IO;
using System.Linq;
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
    IEnvironmentsConfigRealizer environmentsConfigRealizer,
    IEnvironmentsConfigDefaultsProvider defaultsProvider,
    IStateStore stateStore,
    ILogger logger)
    : IConfigSeeder<ControllerModule>
{
    public async Task Execute(CancellationToken stoppingToken)
    {
        // The database is only seeded when it is empty, so anything already authored means this is
        // not a restore: the catalog is already realized and the mirror must not overwrite it.
        var existing = await stateStore.For<AuthoredConfig>().ListAsync(stoppingToken);
        if (existing.Count > 0)
            return;

        var mirror = await ReadMirror(stoppingToken);

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

        // Nothing is saved until the catalog is realized as well: the restored rows and the records
        // they declare go in together, in the single SaveChanges below. Committing the rows first
        // would be one-way — this seeder only runs while nothing is authored, so a failure would
        // leave a restored catalog which nothing ever realizes, and the early return above would
        // skip the retry on every later start.
        await RealizeCatalog(mirror, stoppingToken);

        await stateStore.SaveChangesAsync(stoppingToken);
    }

    private async Task<AuthoredConfigsConfigModel?> ReadMirror(CancellationToken stoppingToken)
    {
        var path = Path.Combine(config.AuthoredConfigsPath, "authored.json");
        if (!fileSystem.File.Exists(path))
            return null;

        try
        {
            fileSystem.File.Copy(path, $"{path}.bak", true);
            var content = await fileSystem.File.ReadAllTextAsync(path, Encoding.UTF8, stoppingToken);
            return JsonSerializer.Deserialize<AuthoredConfigsConfigModel>(content);
        }
        catch (Exception ex)
        {
            throw new SeederException($"Failed to seed database from file '{path}'", ex);
        }
    }

    /// <summary>
    /// Realizes the environment catalog: the sites, and the environments they realize.
    /// </summary>
    /// <remarks>
    /// The catalog is records, so it was dropped with the database and has to exist before anything
    /// is pinned to it — every network realized further down the seeding order resolves its site
    /// from it. The authored value is restored from the mirror when there is one; otherwise this
    /// deployment has never authored a catalog and the host-wired defaults are what is in force
    /// (eryph-zero derives them from agentsettings.yml). Both are local sources: seeding runs long
    /// before any configuration is distributed, so it must not depend on the exchange.
    /// </remarks>
    private async Task RealizeCatalog(
        AuthoredConfigsConfigModel? mirror, CancellationToken stoppingToken)
    {
        var authored = (mirror?.AuthoredConfigs ?? [])
            .FirstOrDefault(a => a.Domain == nameof(ConfigDomain.Environments) && a.Payload is not null);

        EnvironmentsConfig environments;
        if (authored is not null)
        {
            try
            {
                environments = EnvironmentsConfigYamlSerializer.Deserialize(authored.Payload!);
            }
            catch (Exception ex)
            {
                // A mirrored payload which cannot be read is a broken restore, not something to
                // start up around: the catalog it declares would be missing and every resource
                // pinned to it unusable. Fail like the rest of the seeding does.
                throw new SeederException(
                    "Failed to seed the environment catalog from the mirrored configuration", ex);
            }
        }
        else
        {
            environments = await defaultsProvider.GetDefaultEnvironmentsConfig()
                .Match(c => c, error => throw new SeederException(
                    $"Failed to read the default environment configuration: {error.Message}"));
        }

        await environmentsConfigRealizer.RealizeEnvironments(environments, stoppingToken);
    }
}
