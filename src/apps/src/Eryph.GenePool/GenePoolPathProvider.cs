using System;
using System.IO;
using Eryph.Core;
using Eryph.Core.Settings;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Eryph.GenePool;

/// <summary>
/// Resolves the gene pool storage path from the node-local <c>genepoolsettings.yml</c> (under the
/// component config root). That file is the gene pool's local CACHE of the controller-distributed
/// storage config: <c>GenePoolStorageConfigRealizer</c> writes the resolved root into it (the default
/// volumes path plus a <c>genepool</c> folder — the same storage the agent uses), and this provider
/// reads it — the gene-pool counterpart to the agent caching its config in <c>agentsettings.yml</c>.
/// <para>
/// Until the first storage-config push arrives, a default file is written so a fresh node has a usable
/// path; the realizer then overwrites it with the distributed value.
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
