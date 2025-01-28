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
              from config in parseConfigYaml(yaml, false)
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
        let configToSave = normalizePaths(config, hostSettings)
        from yaml in serialize(configToSave)
        from __ in File<RT>.writeAllText(configPath, yaml)
        select unit;

    public static Eff<VmHostAgentConfiguration> parseConfigYaml(
        string yaml,
        bool strict) =>
        from y in Optional(yaml).Filter(notEmpty)
            .ToEff(Error.New("The configuration must not be empty."))
        from config in Eff(() =>
        {
            var builder = new DeserializerBuilder()
                .WithCaseInsensitivePropertyMatching()
                .WithNamingConvention(UnderscoredNamingConvention.Instance);

            if (!strict)
            {
                builder = builder.IgnoreUnmatchedProperties();
            }

            var yamlDeserializer = builder.Build();

            return yamlDeserializer.Deserialize<VmHostAgentConfiguration>(yaml);
        }).MapFail(error => Error.New("The configuration is malformed.", error))
        select config;

    private static Eff<string> serialize(VmHostAgentConfiguration configuration) =>
        Eff(() =>
        {
            var yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            return yamlSerializer.Serialize(configuration);
        });

    private static VmHostAgentConfiguration normalizePaths(
        VmHostAgentConfiguration config,
        HostSettings hostSettings) =>
        new()
        {
            Defaults = simplifyDefaults(normalizePaths(config.Defaults), hostSettings),
            Datastores = config.Datastores?.Select(normalizePaths).ToArray(),
            Environments = config.Environments?.Select(normalizePaths).ToArray(),
        };

    private static VmHostAgentDefaultsConfiguration normalizePaths(
        VmHostAgentDefaultsConfiguration? config) =>
        new()
        {
            Vms = normalizePath(config?.Vms),
            Volumes = normalizePath(config?.Volumes),

        };

    private static VmHostAgentDataStoreConfiguration normalizePaths(
        VmHostAgentDataStoreConfiguration config) =>
        new()
        {
            Name = config.Name,
            Path = normalizePath(config.Path),
        };

    private static VmHostAgentEnvironmentConfiguration normalizePaths(
        VmHostAgentEnvironmentConfiguration config) =>
        new()
        {
            Name = config.Name,
            Defaults = normalizePaths(config.Defaults),
            Datastores = config.Datastores?.Select(normalizePaths).ToArray(),
        };


    private static string? normalizePath(string? path) =>
        path is null ? null : Path.TrimEndingDirectorySeparator(path);

    private static VmHostAgentDefaultsConfiguration simplifyDefaults(
        VmHostAgentDefaultsConfiguration defaults,
        HostSettings hostSettings) =>
        new()
        {
            Vms = string.Equals(
                    Path.TrimEndingDirectorySeparator(hostSettings.DefaultDataPath),
                    defaults.Vms,
                    StringComparison.OrdinalIgnoreCase)
                ? null : defaults.Vms,
            Volumes = string.Equals(
                    Path.TrimEndingDirectorySeparator(hostSettings.DefaultVirtualHardDiskPath),
                    defaults.Volumes,
                    StringComparison.OrdinalIgnoreCase)
                ? null : defaults.Volumes,
        };

    private static VmHostAgentConfiguration applyHostDefaults(
        VmHostAgentConfiguration config,
        HostSettings hostSettings) =>
        new()
        {
            Datastores = config.Datastores,
            Defaults = applyHostDefaults(config.Defaults, hostSettings),
            Environments = config.Environments,
        };

    private static VmHostAgentDefaultsConfiguration applyHostDefaults(
        VmHostAgentDefaultsConfiguration defaults,
        HostSettings hostSettings) =>
        new()
        {
            Vms = Optional(defaults.Vms).Filter(notEmpty).IfNone(hostSettings.DefaultDataPath),
            Volumes = Optional(defaults.Volumes).Filter(notEmpty).IfNone(hostSettings.DefaultVirtualHardDiskPath),
            WatchFileSystem = defaults.WatchFileSystem,
        };
}
