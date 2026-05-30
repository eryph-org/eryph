using System.IO;
using Eryph.Core;
using Eryph.Core.Settings;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.AppCore;

/// <summary>
/// Standalone-runtime implementation of <see cref="IControllerSettingsManager"/>.
/// Reads/writes <c>controllersettings.yml</c> under the component config root
/// (see <see cref="AppConfigPaths"/>), reusing the shared YAML serializer. Mirrors
/// eryph-zero's manager but without the fixed ZeroConfig path.
/// </summary>
public class ControllerSettingsManager : IControllerSettingsManager
{
    private static string ConfigFilePath =>
        Path.Combine(AppConfigPaths.GetControllerSettingsPath(), "controllersettings.yml");

    public EitherAsync<Error, string> GetCurrentConfigurationYaml() =>
        TryAsync(async () =>
        {
            var configFilePath = ConfigFilePath;
            if (!File.Exists(configFilePath))
                await File.WriteAllTextAsync(
                    configFilePath,
                    ControllerSettingsYamlSerializer.Serialize(new ControllerSettings()));

            return await File.ReadAllTextAsync(configFilePath);
        }).ToEither();

    public EitherAsync<Error, ControllerSettings> GetCurrentConfiguration() =>
        from yaml in GetCurrentConfigurationYaml()
        from config in Try(() => ControllerSettingsYamlSerializer.Deserialize(yaml)).ToEitherAsync()
        select config;

    public EitherAsync<Error, Unit> SaveConfigurationYaml(string config) =>
        TryAsync(async () =>
        {
            await File.WriteAllTextAsync(ConfigFilePath, config);
            return unit;
        }).ToEither();

    public EitherAsync<Error, Unit> SaveConfiguration(ControllerSettings config) =>
        from yaml in Try(() => ControllerSettingsYamlSerializer.Serialize(config)).ToEitherAsync()
        from _ in SaveConfigurationYaml(yaml)
        select unit;
}
