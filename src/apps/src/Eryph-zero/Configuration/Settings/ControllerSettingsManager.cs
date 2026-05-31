using System.IO;
using Eryph.Core;
using Eryph.Core.Settings;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero.Configuration.Settings;

public class ControllerSettingsManager : IControllerSettingsManager
{
    public EitherAsync<Error, string> GetCurrentConfigurationYaml() =>
        TryAsync(async () =>
        {
            var path = ZeroConfig.GetControllerSettingsPath();
            Config.EnsurePath(path);

            var configFilePath = Path.Combine(path, "controllersettings.yml");

            if (!File.Exists(configFilePath))
            {
                await File.WriteAllTextAsync(
                    configFilePath,
                    ControllerSettingsYamlSerializer.Serialize(new ControllerSettings()));
            }

            return await File.ReadAllTextAsync(configFilePath);
        }).ToEither();

    public EitherAsync<Error, ControllerSettings> GetCurrentConfiguration() =>
        from yaml in GetCurrentConfigurationYaml()
        from config in Try(() => ControllerSettingsYamlSerializer.Deserialize(yaml))
            .ToEitherAsync()
        select config;

    public EitherAsync<Error, Unit> SaveConfigurationYaml(string config) =>
        TryAsync(async () =>
        {
            var path = ZeroConfig.GetControllerSettingsPath();
            Config.EnsurePath(path);

            var configFilePath = Path.Combine(path, "controllersettings.yml");

            await File.WriteAllTextAsync(configFilePath, config);
            return unit;
        }).ToEither();

    public EitherAsync<Error, Unit> SaveConfiguration(ControllerSettings config) =>
        from yaml in Try(() => ControllerSettingsYamlSerializer.Serialize(config))
            .ToEitherAsync()
        from _ in SaveConfigurationYaml(yaml)
        select unit;
}
