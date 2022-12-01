using Eryph.VmManagement.Networking.Settings;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.Modules.VmHostAgent.Networks;


public class NetworkProviderManager<RT> where RT : struct,
    HasNetworkProviderManager<RT>,
    HasCancel<RT>
{
    // ReSharper disable InconsistentNaming
    public static Aff<RT, string> getCurrentConfigurationYaml() =>
        default(RT).NetworkProviderManager
            .Bind(pm =>
                pm.GetCurrentConfigurationYaml().ToAff(l => l));

    public static Aff<RT, NetworkProvidersConfiguration> getCurrentConfiguration() =>
        default(RT).NetworkProviderManager
            .Bind(pm =>
                pm.GetCurrentConfiguration().ToAff(l => l));


    public static Aff<RT, NetworkProvidersConfiguration> parseConfigurationYaml(string yaml) =>
        default(RT).NetworkProviderManager
            .Bind(pm =>
                pm.ParseConfigurationYaml(yaml).ToAff(l => l));


    public static Aff<RT, Unit> saveConfigurationYaml(string config) =>
        default(RT).NetworkProviderManager
            .Bind(pm =>
                pm.SaveConfigurationYaml(config).ToAff(l => l));

    // ReSharper restore InconsistentNaming

}