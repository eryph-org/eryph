using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Builds the <see cref="ConfigDomain.StorageConfig"/> payload. The operator-authored value (set via
/// the management API and stored versioned) is authoritative once it exists; until the domain is first
/// authored the source falls back to the Placement section of the controller settings file, so
/// existing file-based deployments keep working unchanged.
/// </summary>
internal sealed class StorageConfigSource(
    Container container,
    IControllerSettingsManager settingsManager,
    ILogger<StorageConfigSource> logger)
    : IConfigSource
{
    public ConfigDomain Domain => ConfigDomain.StorageConfig;

    public async Task<string> BuildPayloadAsync(CancellationToken cancellationToken)
    {
        // The authored value (operator-set via the management API) is authoritative once it exists.
        // The store is scoped, so resolve it in a dedicated scope — this source may be built outside a
        // request scope (mirrors EndpointsConfigSource).
        await using (var scope = AsyncScopedLifestyle.BeginScope(container))
        {
            var authored = await scope.GetInstance<IAuthoredConfigStore>()
                .GetCurrentAsync(ConfigDomain.StorageConfig, ConfigScope.Default, cancellationToken);
            if (authored is not null)
                return authored.Payload;
        }

        // Not yet authored via the management API — fall back to the controller settings file.
        return await settingsManager.GetCurrentConfiguration()
            .Match(
                settings => StorageConfigYamlSerializer.Serialize(settings.Storage),
                error =>
                {
                    // Never distribute a silently-empty placement vocabulary — that would make
                    // agents reject every non-default datastore/environment. Fail the round
                    // instead (mirrors NetworkProvidersConfigSource); agents keep their current
                    // copy until the controller settings are readable again.
                    logger.LogError(
                        "Failed to read controller settings for {Domain}: {Error}.",
                        ConfigDomain.StorageConfig, error.Message);
                    throw new InvalidOperationException(
                        $"Cannot distribute placement configuration: {error.Message}");
                });
    }
}
