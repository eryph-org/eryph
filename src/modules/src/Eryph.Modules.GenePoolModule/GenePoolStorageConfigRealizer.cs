using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Settings;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.GenePool;

/// <summary>
/// Applies the controller-distributed storage configuration to the gene pool: the gene-pool root is the
/// default volumes path plus a <c>genepool</c> folder (the same rule the agent uses), so the resolved
/// root is written to the local <c>genepoolsettings.yml</c> cache that <c>GenePoolPathProvider</c>
/// reads. This makes central config the source of the gene-pool storage location instead of the gene
/// pool borrowing the agent's settings or duplicating them in a separate operator-edited file.
/// </summary>
internal sealed class GenePoolStorageConfigRealizer(
    IGenePoolStorageSettingsManager settingsManager,
    ILogger<GenePoolStorageConfigRealizer> logger)
    : IConfigRealizer
{
    // The gene pool lives under the default volumes datastore; keep this in step with
    // HyperVGenePoolPaths.GetGenePoolPath.
    private const string GenePoolFolderName = "genepool";

    public ConfigDomain Domain => ConfigDomain.StorageConfig;

    public async Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        var config = StorageConfigYamlSerializer.Deserialize(payload);

        var volumes = config.Defaults?.Volumes;
        if (string.IsNullOrWhiteSpace(volumes))
        {
            // Nothing to derive the gene-pool root from; leave the local cache as-is (last-known or
            // default) rather than clearing it, so the gene pool keeps working.
            logger.LogWarning(
                "Applied storage configuration v{Version} has no default volumes path; "
                + "the gene pool storage path is left unchanged.", version);
            return;
        }

        var genePoolPath = Path.Combine(volumes, GenePoolFolderName);

        // A write failure must propagate: ConfigApplier turns the thrown exception into a failed
        // ConfigAppliedEvent so the controller retries, rather than believing the path was applied.
        await settingsManager.SaveSettings(new GenePoolStoreSettings { Path = genePoolPath })
            .Match(
                _ =>
                {
                    logger.LogInformation(
                        "Applied storage configuration v{Version}: gene pool path set to {Path}.",
                        version, genePoolPath);
                    return unit;
                },
                error => throw new InvalidOperationException(
                    $"Failed to write the gene pool storage path: {error.Message}"));
    }
}
