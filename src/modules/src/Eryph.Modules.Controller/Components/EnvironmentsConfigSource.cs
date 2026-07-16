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
/// Builds the <see cref="ConfigDomain.Environments"/> payload from the operator-authored value (set
/// via the management API and stored versioned).
/// </summary>
/// <remarks>
/// Until the domain is first authored the catalog comes from the host-wired
/// <see cref="IEnvironmentsConfigDefaultsProvider"/>: eryph-zero has always declared its environments
/// in <c>agentsettings.yml</c>, so they must keep working without the operator authoring anything,
/// while the split runtime defines them centrally and therefore starts with the reserved default alone.
/// </remarks>
internal sealed class EnvironmentsConfigSource(
    Container container,
    IEnvironmentsConfigDefaultsProvider defaultsProvider,
    ILogger<EnvironmentsConfigSource> logger)
    : IConfigSource
{
    public ConfigDomain Domain => ConfigDomain.Environments;

    public async Task<string> BuildPayloadAsync(string scope, CancellationToken cancellationToken)
    {
        // The store is scoped, so resolve it in a dedicated scope — this source may be built outside
        // a request scope (mirrors StorageConfigSource).
        await using (var diScope = AsyncScopedLifestyle.BeginScope(container))
        {
            var authored = await diScope.GetInstance<IAuthoredConfigStore>()
                .GetCurrentAsync(ConfigDomain.Environments, scope, cancellationToken);
            if (authored is not null)
                return authored.Payload;
        }

        // Environments are global, so only the default scope is ever materialized.
        if (scope != ConfigScope.Default)
            throw new InvalidOperationException($"No authored environment configuration for scope '{scope}'.");

        // Not yet authored via the management API — fall back to the host-wired defaults source.
        var config = await defaultsProvider.GetDefaultEnvironmentsConfig()
            .Match(
                c => c,
                error =>
                {
                    // Never distribute a silently-empty catalog — that would make every deployment
                    // into an existing environment fail as unknown (mirrors StorageConfigSource).
                    // Agents keep their current copy until the source is readable again.
                    logger.LogError(
                        "Failed to read the default environment configuration for {Domain}: {Error}.",
                        ConfigDomain.Environments, error.Message);
                    throw new InvalidOperationException(
                        $"Cannot distribute the environment configuration: {error.Message}");
                });

        // Run the derived catalog through the same canonicalization the authored path uses, so a
        // malformed agentsettings.yml fails here with a clear error instead of being distributed
        // verbatim and rejected on every agent.
        if (!ConfigDomainDescriptors.TryCanonicalize(
                ConfigDomain.Environments, EnvironmentsConfigYamlSerializer.Serialize(config),
                out var canonical, out var canonicalizeError))
        {
            logger.LogError(
                "The default environment configuration for {Domain} is invalid: {Error}.",
                ConfigDomain.Environments, canonicalizeError);
            throw new InvalidOperationException(
                $"Cannot distribute the environment configuration: {canonicalizeError}");
        }

        return canonical;
    }
}
