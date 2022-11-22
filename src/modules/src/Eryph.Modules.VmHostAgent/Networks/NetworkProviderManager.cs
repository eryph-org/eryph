using System;
using System.IO;
using Eryph.Core;
using Eryph.Modules.VmHostAgent.Networks.Settings;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Effects.Traits;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Eryph.Modules.VmHostAgent.Networks;

public class NetworkProviderManager : INetworkProviderManager
{
    public EitherAsync<Error,string> GetCurrentConfigurationYaml()
    {
        return Prelude.TryAsync(async () =>
        {

            var path = Path.Combine(Config.GetConfigPath("hostagent"), "networks");
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

    public Either<Error,NetworkProvidersConfiguration> ParseConfigurationYaml(string yaml)
    {
        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        return Prelude.Try(() => {
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
                var path = Path.Combine(Config.GetConfigPath("hostagent"), "networks");
                Config.EnsurePath(path);

                var configFilePath = Path.Combine(path, "p_networks.yml");

                await File.WriteAllTextAsync(configFilePath, config);
                return Unit.Default;
            }
        ).ToEither();

    }

}

public class NetworkProviderManager<RT> where RT : struct, 
    HasNetworkProviderManager<RT>,
    HasCancel<RT>
{
    // ReSharper disable InconsistentNaming
    public static Aff<RT,string> getCurrentConfigurationYaml() =>
        default(RT).NetworkProviderManager
            .Bind(pm =>
                pm.GetCurrentConfigurationYaml().ToAff(l => l));

    public static Aff<RT, NetworkProvidersConfiguration> getCurrentConfiguration() =>
        default(RT).NetworkProviderManager
            .Bind(pm =>
                pm.GetCurrentConfiguration().ToAff(l => l));


    public static Aff<RT,NetworkProvidersConfiguration> parseConfigurationYaml(string yaml) =>
        default(RT).NetworkProviderManager
            .Bind(pm =>
                pm.ParseConfigurationYaml(yaml).ToAff(l => l));


    public static Aff<RT, Unit> saveConfigurationYaml(string config) =>
        default(RT).NetworkProviderManager
            .Bind(pm =>
                pm.SaveConfigurationYaml(config).ToAff(l => l));

    // ReSharper restore InconsistentNaming

}