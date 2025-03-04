using System;
using System.IO;
using Eryph.Core;
using Eryph.Core.Network;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero.Configuration.Networks;

public class NetworkProviderManager : INetworkProviderManager
{
    public EitherAsync<Error, string> GetCurrentConfigurationYaml() =>
        TryAsync(async () =>
        {
            var path = ZeroConfig.GetNetworksConfigPath();
            Config.EnsurePath(path);

            var configFilePath = Path.Combine(path, "p_networks.yml");

            if (!File.Exists(configFilePath))
            {
                await File.WriteAllTextAsync(configFilePath, NetworkProvidersConfiguration.DefaultConfig);
            }

            return await File.ReadAllTextAsync(configFilePath);
        }).ToEither();

    public EitherAsync<Error, NetworkProvidersConfiguration> GetCurrentConfiguration() =>
        from yaml in GetCurrentConfigurationYaml()
        from config in Try(() => NetworkProvidersConfigYamlSerializer.Deserialize(yaml))
            .ToEitherAsync()
        select config;

    public EitherAsync<Error, Unit> SaveConfigurationYaml(string config) =>
        TryAsync(async () =>
        {
            var path = ZeroConfig.GetNetworksConfigPath();
            Config.EnsurePath(path);

            var configFilePath = Path.Combine(path, "p_networks.yml");

            await File.WriteAllTextAsync(configFilePath, config);
            return unit;
        }).ToEither();

    public EitherAsync<Error, Unit> SaveConfiguration(
        NetworkProvidersConfiguration config) =>
        from yaml in Try(() => NetworkProvidersConfigYamlSerializer.Serialize(config))
            .ToEitherAsync()
        from _ in SaveConfigurationYaml(yaml)
        select unit;
}
