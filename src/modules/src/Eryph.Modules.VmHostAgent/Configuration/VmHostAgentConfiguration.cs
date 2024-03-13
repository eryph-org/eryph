using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.VmAgent;
using LanguageExt.Common;
using LanguageExt.Sys.IO;
using LanguageExt;
using LanguageExt.Sys.Traits;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Configuration;

public static class VmHostAgentConfiguration<RT> where RT : struct,
    HasFile<RT>,
    HasDirectory<RT>
{
    public static Aff<RT, string> getConfigYaml(
        string configPath,
        HostSettings hostSettings) =>
        from config in readConfig(configPath, hostSettings)
        from yaml in serialize(config)
        select yaml;

    public static Aff<RT, VmHostAgentConfiguration> readConfig(
        string configPath,
        HostSettings hostSettings) =>
        from fileExists in File<RT>.exists(configPath)
        from config in fileExists
            ? from yaml in File<RT>.readAllText(configPath)
              from config in parseConfigYaml(yaml)
              select config
            : from config in SuccessEff(new VmHostAgentConfiguration())
              from _ in saveConfig(config, configPath, hostSettings)
              select config
        select applyHostDefaults(config, hostSettings);

    public static Aff<RT, Unit> saveConfig(
        VmHostAgentConfiguration config,
        string configPath,
        HostSettings hostSettings) =>
        from configDirectory in Optional(Path.GetDirectoryName(configPath))
            .Filter(notEmpty)
            .ToAff(Error.New("The config path is invalid"))
        from _ in Directory<RT>.create(configDirectory)
        from configToSave in SuccessEff(new VmHostAgentConfiguration()
        {
            Defaults = simplifyDefaults(config.Defaults, hostSettings),
            Datastores = config.Datastores,
            Environments = config.Environments?.Select(
                    e => new VmHostAgentEnvironmentConfiguration()
                    {
                        Datastores = e.Datastores,
                        Defaults = simplifyDefaults(e.Defaults, hostSettings),
                        Name = e.Name,
                    })
                .ToArray(),
        })
        from yaml in serialize(configToSave)
        from __ in File<RT>.writeAllText(configPath, yaml)
        select unit;

    public static Eff<VmHostAgentConfiguration> parseConfigYaml(string yaml) =>
        Eff(() =>
        {
            var yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            return yamlDeserializer.Deserialize<VmHostAgentConfiguration>(yaml);
        });

    private static Eff<string> serialize(VmHostAgentConfiguration configuration) =>
        Eff(() =>
        {
            var yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            return yamlSerializer.Serialize(configuration);
        });

    private static VmHostAgentDefaultsConfiguration simplifyDefaults(
        VmHostAgentDefaultsConfiguration defaults,
        HostSettings hostSettings) =>
        new()
        {
            Vms = hostSettings.DefaultDataPath == defaults.Vms ? null : defaults.Vms,
            Volumes = hostSettings.DefaultVirtualHardDiskPath == defaults.Volumes ? null : defaults.Volumes,
        };

    private static VmHostAgentConfiguration applyHostDefaults(
        VmHostAgentConfiguration config,
        HostSettings hostSettings) =>
        new()
        {
            Datastores = config.Datastores,
            Defaults = applyHostDefaults(config.Defaults, hostSettings),
            Environments = config.Environments?.Select(env => new VmHostAgentEnvironmentConfiguration()
            {
                Datastores = env.Datastores,
                Defaults = applyHostDefaults(env.Defaults, hostSettings),
                Name = env.Name,
            }).ToArray(),
        };

    private static VmHostAgentDefaultsConfiguration applyHostDefaults(
        VmHostAgentDefaultsConfiguration defaults,
        HostSettings hostSettings) =>
        new()
        {
            Vms = Optional(defaults.Vms).Filter(notEmpty).IfNone(hostSettings.DefaultDataPath),
            Volumes = Optional(defaults.Volumes).Filter(notEmpty).IfNone(hostSettings.DefaultVirtualHardDiskPath),
        };
}
