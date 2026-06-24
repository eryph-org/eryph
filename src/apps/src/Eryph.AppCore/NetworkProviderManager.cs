using System.IO;
using Eryph.Core;
using Eryph.Core.Network;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph;

/// <summary>
/// Standalone-runtime implementation of <see cref="INetworkProviderManager"/>. Reads/writes
/// <c>p_networks.yml</c> under the component config root (see <see cref="AppConfigPaths"/>),
/// reusing the shared YAML serializer. Mirrors eryph-zero's manager but without the fixed
/// ZeroConfig path.
/// </summary>
public class NetworkProviderManager : INetworkProviderManager
{
    private static string ConfigFilePath =>
        Path.Combine(AppConfigPaths.GetNetworksConfigPath(), "p_networks.yml");

    public EitherAsync<Error, string> GetCurrentConfigurationYaml() =>
        TryAsync(async () =>
        {
            var configFilePath = ConfigFilePath;
            if (!File.Exists(configFilePath))
                await File.WriteAllTextAsync(configFilePath, NetworkProvidersConfiguration.DefaultConfig);

            return await File.ReadAllTextAsync(configFilePath);
        }).ToEither();

    public EitherAsync<Error, NetworkProvidersConfiguration> GetCurrentConfiguration() =>
        from yaml in GetCurrentConfigurationYaml()
        from config in Try(() => NetworkProvidersConfigYamlSerializer.Deserialize(yaml)).ToEitherAsync()
        select config;

    public EitherAsync<Error, Unit> SaveConfigurationYaml(string config) =>
        TryAsync(async () =>
        {
            await File.WriteAllTextAsync(ConfigFilePath, config);
            return unit;
        }).ToEither();

    public EitherAsync<Error, Unit> SaveConfiguration(NetworkProvidersConfiguration config) =>
        from yaml in Try(() => NetworkProvidersConfigYamlSerializer.Serialize(config)).ToEitherAsync()
        from _ in SaveConfigurationYaml(yaml)
        select unit;

    public NetworkProviderDefaults Defaults => new()
    {
        DisableDhcpGuard = true,
        DisableRouterGuard = true,
        MacAddressSpoofing = true,
    };
}
