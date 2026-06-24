using System;
using System.IO;
using Eryph.Core;
using Eryph.Core.Settings;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Eryph.GenePool;

/// <summary>
/// Resolves the gene pool storage path from the node-local <c>genepoolsettings.yml</c> (under the
/// component config root). This is the gene-pool counterpart to the agent reading its
/// <c>agentsettings.yml</c>, and the split that separates the gene pool's storage configuration from
/// the agent's: the gene pool owns its own datastore setting instead of deriving it from the agent's
/// host settings.
/// <para>
/// The file is the LOCAL copy of what the controller will distribute mid-term (groupable per
/// environment); only local resolution is implemented now. A default file is written on first use so
/// an operator has something to edit.
/// </para>
/// </summary>
internal sealed class GenePoolPathProvider : IGenePoolPathProvider
{
    private static string ConfigFilePath =>
        Path.Combine(AppConfigPaths.GetGenePoolSettingsPath(), "genepoolsettings.yml");

    private static string DefaultGenePoolPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "eryph", "genepool");

    public Aff<string> GetGenePoolPath() =>
        from settings in ReadOrCreateSettings()
        select string.IsNullOrWhiteSpace(settings.Path) ? DefaultGenePoolPath : settings.Path;

    private static Aff<GenePoolStoreSettings> ReadOrCreateSettings() =>
        Aff(async () =>
        {
            var configFilePath = ConfigFilePath;
            if (!File.Exists(configFilePath))
            {
                var defaults = new GenePoolStoreSettings { Path = DefaultGenePoolPath };
                await File.WriteAllTextAsync(
                    configFilePath, GenePoolStoreSettingsYamlSerializer.Serialize(defaults));
                return defaults;
            }

            var yaml = await File.ReadAllTextAsync(configFilePath);
            return GenePoolStoreSettingsYamlSerializer.Deserialize(yaml);
        });
}
