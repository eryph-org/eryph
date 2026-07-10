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
            // Nothing to derive the gene-pool root from. Report success (not failure): the volumes path
            // may legitimately be unauthored, and failing would make the controller retry the same
            // payload forever. Leave the local cache as-is (last-known, or the provider's default on a
            // fresh node) so the gene pool keeps working; a later push that sets the path corrects it.
            logger.LogWarning(
                "Applied storage configuration v{Version} has no default volumes path; "
                + "the gene pool storage path is left unchanged.", version);
            return;
        }

        // The controller validates authored paths, but the gene pool is a second consumer of the same
        // payload and must not be the least-validated writer: reject a non-fully-qualified volumes path
        // rather than write a cwd-relative gene-pool root while the agent rejects the same payload. Use
        // the same OS-agnostic check as the controller/agent — the gene pool may run cross-platform, and
        // System.IO.Path.IsPathFullyQualified would reject a valid Windows path when evaluated on Linux.
        if (!StorageConfigValidation.IsFullyQualifiedPath(volumes))
            throw new InvalidOperationException(
                $"The distributed default volumes path '{volumes}' is not fully qualified; "
                + "the gene pool storage path cannot be derived from it.");

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
