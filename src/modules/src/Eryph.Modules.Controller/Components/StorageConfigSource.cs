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
/// authored the source falls back to the host-wired <see cref="IStorageConfigDefaultsProvider"/> — the
/// central controller settings in the split runtime, or the local <c>agentsettings.yml</c> in
/// eryph-zero — so the same distributed config feeds both the agent and the gene pool.
/// </summary>
internal sealed class StorageConfigSource(
    Container container,
    IStorageConfigDefaultsProvider defaultsProvider,
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

        // Not yet authored via the management API — fall back to the host-wired defaults source.
        var config = await defaultsProvider.GetDefaultStorageConfig()
            .Match(
                c => c,
                error =>
                {
                    // Never distribute a silently-empty storage vocabulary — that would make agents
                    // reject every non-default datastore/environment. Fail the round instead (mirrors
                    // NetworkProvidersConfigSource); agents keep their current copy until the source is
                    // readable again.
                    logger.LogError(
                        "Failed to read the default storage configuration for {Domain}: {Error}.",
                        ConfigDomain.StorageConfig, error.Message);
                    throw new InvalidOperationException(
                        $"Cannot distribute the storage configuration: {error.Message}");
                });

        // Run the settings-derived config through the same canonicalization (name lower-casing +
        // validation) the authored path uses, so a mixed-case or invalid controllersettings/agentsettings
        // Storage section fails here with a clear error instead of being distributed verbatim and rejected
        // on every agent — the source must not be the least-validated writer.
        if (!ConfigDomainDescriptors.TryCanonicalize(
                ConfigDomain.StorageConfig, StorageConfigYamlSerializer.Serialize(config),
                out var canonical, out var canonicalizeError))
        {
            logger.LogError(
                "The default storage configuration for {Domain} is invalid: {Error}.",
                ConfigDomain.StorageConfig, canonicalizeError);
            throw new InvalidOperationException(
                $"Cannot distribute the storage configuration: {canonicalizeError}");
        }

        return canonical;
    }
}
