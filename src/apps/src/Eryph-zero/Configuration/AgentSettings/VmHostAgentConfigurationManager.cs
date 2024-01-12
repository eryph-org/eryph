using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Eryph.Runtime.Zero.Configuration.AgentSettings
{
    internal class VmHostAgentConfigurationManager : IVmHostAgentConfigurationManager
    {
        public EitherAsync<Error, string> GetCurrentConfigurationYaml(
            HostSettings hostSettings)
        {
            return from config in GetCurrentConfiguration(hostSettings)
                   select SerializeConfiguration(config);
        }

        public EitherAsync<Error, VmHostAgentConfiguration> GetCurrentConfiguration(
            HostSettings hostSettings)
        {
            return from config in ReadConfigurationFromDisk()
                .Bind(yaml => yaml.Match(
                    yaml => ParseConfigurationYaml(yaml).ToAsync(),
                    () => from config in Prelude.TryAsync(new VmHostAgentConfiguration()).ToEither()
                               from _ in SaveConfiguration(config, hostSettings)
                               select config
                    ))
                   select ApplyHostDefaults(config, hostSettings);
        }

        public Either<Error, VmHostAgentConfiguration> ParseConfigurationYaml(string yaml)
        {
            var yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            return Prelude.Try(() =>
                {
                    try
                    {
                        return yamlDeserializer.Deserialize<VmHostAgentConfiguration>(yaml);
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

        public EitherAsync<Error, Unit> SaveConfiguration(
            VmHostAgentConfiguration configuration,
            HostSettings hostSettings)
        {
            return Prelude.TryAsync(async () =>
            {
                var path = ZeroConfig.GetVmHostAgentConfigPath();
                Config.EnsurePath(path);

                var configFilePath = Path.Combine(path, "agentsettings.yml");

                var configToSafe = new VmHostAgentConfiguration()
                {
                    Defaults = SimplifyDefaults(configuration.Defaults, hostSettings),
                    Datastores = configuration.Datastores,
                    Environments = configuration.Environments.Select(e => new VmHostAgentEnvironmentConfiguration()
                    {
                        Datastores = e.Datastores,
                        Defaults = SimplifyDefaults(e.Defaults, hostSettings),
                        Name = e.Name,
                    }).ToArray(),
                };

                string yaml = SerializeConfiguration(configToSafe);

                await File.WriteAllTextAsync(configFilePath, yaml);
                return Unit.Default;
            }
            ).ToEither();
        }

        private string SerializeConfiguration(VmHostAgentConfiguration configuration)
        {
            var yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            return yamlSerializer.Serialize(configuration);
        }

        public EitherAsync<Error, Unit> SaveConfigurationYaml(string config)
        {
            return Prelude.TryAsync(async () =>
                {
                    var path = ZeroConfig.GetVmHostAgentConfigPath();
                    Config.EnsurePath(path);

                    var configFilePath = Path.Combine(path, "agentsettings.yml");

                    await File.WriteAllTextAsync(configFilePath, config);
                    return Unit.Default;
                }
            ).ToEither();
        }

        private EitherAsync<Error, Option<string>> ReadConfigurationFromDisk()
        {
            return Prelude.TryAsync<Option<string>>(async () =>
            {
                var path = ZeroConfig.GetVmHostAgentConfigPath();
                Config.EnsurePath(path);

                var configFilePath = Path.Combine(path, "agentsettings.yml");

                if (!File.Exists(configFilePath))
                    return Prelude.None;

                return await File.ReadAllTextAsync(configFilePath);
            }).ToEither();
        }

        private static VmHostAgentDefaultsConfiguration SimplifyDefaults(
            VmHostAgentDefaultsConfiguration defaults,
            HostSettings hostSettings)
        {
            return new()
            {
                Vms = hostSettings.DefaultDataPath == defaults.Vms
                    ? null: defaults.Vms,
                Volumes = hostSettings.DefaultVirtualHardDiskPath == defaults.Volumes
                    ? null : defaults.Volumes,
            };
        }

        private static VmHostAgentConfiguration ApplyHostDefaults(
            VmHostAgentConfiguration config,
            HostSettings hostSettings)
        {
            return new()
            {
                Datastores = config.Datastores,
                Defaults = ApplyHostDefaults(config.Defaults, hostSettings),
                Environments = config.Environments.Select(env => new VmHostAgentEnvironmentConfiguration()
                {
                    Datastores = env.Datastores,
                    Defaults = ApplyHostDefaults(env.Defaults, hostSettings),
                    Name = env.Name,
                }).ToArray(),
            };
        }

        private static VmHostAgentDefaultsConfiguration ApplyHostDefaults(
            VmHostAgentDefaultsConfiguration defaults,
            HostSettings hostSettings)
        {
            return new()
            {
                Vms = string.IsNullOrEmpty(defaults.Vms) ? hostSettings.DefaultDataPath : defaults.Vms,
                Volumes = string.IsNullOrEmpty(defaults.Volumes) ? hostSettings.DefaultVirtualHardDiskPath : defaults.Volumes,
            };
        }
    }
}
