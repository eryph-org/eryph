using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Builds the <see cref="ConfigDomain.Environments"/> payload from the operator-authored value (set
/// via the management API and stored versioned).
/// </summary>
/// <remarks>
/// Unlike <see cref="StorageConfigSource"/> there is no settings-file fallback: an environment has no
/// host-local half to derive a default from, and the default environment is reserved and always
/// resolves to the default site without being listed. Until the domain is first authored, the
/// deployment therefore has exactly the default environment, which an empty catalog expresses
/// faithfully.
/// </remarks>
internal sealed class EnvironmentsConfigSource(
    Container container)
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

        return EnvironmentsConfigYamlSerializer.Serialize(new EnvironmentsConfig());
    }
}
