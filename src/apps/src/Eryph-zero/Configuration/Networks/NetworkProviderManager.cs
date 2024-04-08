using System;
using System.IO;
using Eryph.Core;
using Eryph.Core.Network;
using LanguageExt;
using LanguageExt.Common;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Eryph.Runtime.Zero.Configuration.Networks;

public class NetworkProviderManager : INetworkProviderManager
{
    public EitherAsync<Error, string> GetCurrentConfigurationYaml()
    {
        return Prelude.TryAsync(async () =>
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
    }

    public EitherAsync<Error, NetworkProvidersConfiguration> GetCurrentConfiguration()
    {
        return from yaml in GetCurrentConfigurationYaml()
               from config in ParseConfigurationYaml(yaml).ToAsync()
               select config;

    }

    public Either<Error, NetworkProvidersConfiguration> ParseConfigurationYaml(string yaml)
    {
        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        return Prelude.Try(() =>
        {
            try
            {
                return yamlDeserializer.Deserialize<NetworkProvidersConfiguration>(yaml);
            }
            catch (Exception ex)
            {
                if (ex.InnerException == null)
                    throw;

                throw ex.InnerException;
            }
        })
            .ToEither(Error.New);

    }

    public EitherAsync<Error, Unit> SaveConfigurationYaml(string config)
    {
        return Prelude.TryAsync(async () =>
            {
                var path = ZeroConfig.GetNetworksConfigPath();
                Config.EnsurePath(path);

                var configFilePath = Path.Combine(path, "p_networks.yml");

                await File.WriteAllTextAsync(configFilePath, config);
                return Unit.Default;
            }
        ).ToEither();
    }

    public EitherAsync<Error, Unit> SaveConfiguration(
        NetworkProvidersConfiguration config) =>
        from yaml in Prelude.Try(() =>
        {
            var yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            return yamlSerializer.Serialize(config);
        }).ToEitherAsync()
        from _ in SaveConfigurationYaml(yaml)
        select Unit.Default;
}
