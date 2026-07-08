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

    public async Task<string> BuildPayloadAsync(string scope, CancellationToken cancellationToken)
    {
        // The authored value (operator-set via the management API) at this scope is authoritative.
        // The store is scoped, so resolve it in a dedicated scope — this source may be built outside a
        // request scope (mirrors EndpointsConfigSource).
        await using (var diScope = AsyncScopedLifestyle.BeginScope(container))
        {
            var authored = await diScope.GetInstance<IAuthoredConfigStore>()
                .GetCurrentAsync(ConfigDomain.StorageConfig, scope, cancellationToken);
            if (authored is not null)
                return authored.Payload;
        }

        // A non-default scope is only materialized when it has an authored value, so an unauthored
        // non-default scope is a caller error; the default scope falls back to the settings file.
        if (scope != ConfigScope.Default)
            throw new InvalidOperationException($"No authored StorageConfig for scope '{scope}'.");

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
