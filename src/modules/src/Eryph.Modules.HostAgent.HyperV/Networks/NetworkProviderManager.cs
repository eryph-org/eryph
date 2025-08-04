using Eryph.Core.Network;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.Modules.HostAgent.Networks;

public class NetworkProviderManager<RT> where RT : struct,
    HasNetworkProviderManager<RT>,
    HasCancel<RT>
{
    public static Aff<RT, string> getCurrentConfigurationYaml() =>
        default(RT).NetworkProviderManager
            .Bind(pm => pm.GetCurrentConfigurationYaml().ToAff(l => l));

    public static Aff<RT, NetworkProvidersConfiguration> getCurrentConfiguration() =>
        default(RT).NetworkProviderManager
            .Bind(pm => pm.GetCurrentConfiguration().ToAff(l => l));


    public static Aff<RT, Unit> saveConfigurationYaml(string config) =>
        default(RT).NetworkProviderManager
            .Bind(pm => pm.SaveConfigurationYaml(config).ToAff(l => l));

    public static Eff<RT, NetworkProviderDefaults> getDefaults() =>
        default(RT).NetworkProviderManager.Map(pm => pm.Defaults);
}
