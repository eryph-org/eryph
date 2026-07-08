using System.IO;
using Eryph.Core;
using Eryph.Core.Settings;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.AppCore;

/// <summary>
/// File-backed gene-pool storage settings: reads/writes <c>genepoolsettings.yml</c> under the component
/// config root. Used by the standalone gene pool, where the file is the local cache of the distributed
/// storage config (written by the realizer, read by <c>GenePoolPathProvider</c>).
/// </summary>
public sealed class GenePoolStorageSettingsManager : IGenePoolStorageSettingsManager
{
    private static string ConfigFilePath =>
        Path.Combine(AppConfigPaths.GetGenePoolSettingsPath(), "genepoolsettings.yml");

    public EitherAsync<Error, GenePoolStoreSettings> GetCurrentSettings() =>
        TryAsync(async () =>
        {
            if (!File.Exists(ConfigFilePath))
                return new GenePoolStoreSettings();

            var yaml = await File.ReadAllTextAsync(ConfigFilePath);
            return GenePoolStoreSettingsYamlSerializer.Deserialize(yaml);
        }).ToEither();

    public EitherAsync<Error, Unit> SaveSettings(GenePoolStoreSettings settings) =>
        TryAsync(async () =>
        {
            await File.WriteAllTextAsync(
                ConfigFilePath, GenePoolStoreSettingsYamlSerializer.Serialize(settings));
            return unit;
        }).ToEither();
}
