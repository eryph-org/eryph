using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// The environment catalog which is currently in force: the operator-authored value once one exists,
/// the host-wired defaults until then.
/// </summary>
/// <remarks>
/// This is the single seam that keeps every controller-side consumer and the config source in step, so
/// authoring cannot make the controller diverge from what agents receive. Reading the authored store
/// directly would answer "unknown environment" for a deployment whose agents were handed a catalog
/// containing it. Mirrors <see cref="AuthoredNetworkProviderManager"/>, which does the same for the
/// network providers.
/// </remarks>
internal interface ICurrentEnvironmentsConfig
{
    Task<EnvironmentsConfig> GetAsync(CancellationToken cancellationToken);
}

internal sealed class CurrentEnvironmentsConfig(
    IAuthoredConfigStore authoredConfigStore,
    IEnvironmentsConfigDefaultsProvider defaultsProvider)
    : ICurrentEnvironmentsConfig
{
    public async Task<EnvironmentsConfig> GetAsync(CancellationToken cancellationToken)
    {
        var authored = await authoredConfigStore.GetCurrentAsync(
            ConfigDomain.Environments, ConfigScope.Default, cancellationToken);
        if (authored is not null)
            return EnvironmentsConfigYamlSerializer.Deserialize(authored.Payload);

        return await defaultsProvider.GetDefaultEnvironmentsConfig()
            .Match(c => c, error => throw new System.InvalidOperationException(
                $"Cannot read the default environment configuration: {error.Message}"));
    }
}
