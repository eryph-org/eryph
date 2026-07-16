using System.Linq;
using Eryph.Core;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Runtime.Zero;

/// <summary>
/// eryph-zero default environment catalog: derived from the local <c>agentsettings.yml</c>, where an
/// environment has always been declared to bind its storage paths. eryph on-premises spans exactly one
/// site, so every environment the operator declared locally is realized by the default site.
/// </summary>
/// <remarks>
/// Without this, adding the environment catalog would silently un-declare the environments an existing
/// deployment already uses: the catalog would hold only the reserved default, and every deployment into
/// an environment from <c>agentsettings.yml</c> would be refused as unknown. Mirrors
/// <see cref="AgentSettingsStorageConfigDefaultsProvider"/>, which does the same for the storage half.
/// </remarks>
internal sealed class AgentSettingsEnvironmentsConfigDefaultsProvider(
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
    : IEnvironmentsConfigDefaultsProvider
{
    public EitherAsync<Error, EnvironmentsConfig> GetDefaultEnvironmentsConfig() =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from config in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        select ToEnvironmentsConfig(config);

    private static EnvironmentsConfig ToEnvironmentsConfig(VmHostAgentConfiguration config) =>
        new()
        {
            // The sites are not derived: on-premises has the default site and nothing else, and it is
            // reserved, so declaring it would be rejected as an authored value.
            Sites = [],
            Environments = (config.Environments ?? [])
                .Select(e => e.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                // The default environment is reserved and always resolves to the default site, so it
                // is never listed — an agentsettings.yml which declares it explicitly (to bind its
                // paths) must not turn the derived catalog into a rejected one.
                .Where(n => !string.Equals(
                    n.Trim(), EryphConstants.DefaultEnvironmentName, System.StringComparison.OrdinalIgnoreCase))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .Select(n => new EnvironmentConfig
                {
                    Name = n.Trim(),
                    Site = EryphConstants.DefaultSiteName,
                })
                .ToArray(),
        };
}
