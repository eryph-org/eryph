using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.HostAgent.Inventory;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Applies the controller-distributed storage configuration: the datastore and environment vocabulary
/// the agent may serve, together with the concrete paths they map to. The received vocabulary is
/// recorded so provisioning can enforce it, and the distributed paths are merged over the local
/// <c>agentsettings.yml</c> (distributed wins) and written back, so the file becomes a cache of the
/// distributed config and all path resolution keeps going through the single existing seam. Local
/// datastores/environments the controller does not know are warned about — they can never be placed on.
/// </summary>
internal sealed class StorageConfigRealizer(
    IStorageConfigProvider storageConfigProvider,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    IDiskStoresChangeWatcher diskStoresChangeWatcher,
    ILogger<StorageConfigRealizer> logger)
    : IConfigRealizer
{
    public ConfigDomain Domain => ConfigDomain.StorageConfig;

    public async Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        var config = StorageConfigYamlSerializer.Deserialize(payload);

        // Merge/validate/save first; only switch the enforced vocabulary and report success once the
        // cache was actually written. Updating the provider before a failed save would make the agent
        // reject placements against a vocabulary it never persisted while the controller sees the apply
        // as failed.
        await MergeIntoLocalCache(config);

        storageConfigProvider.Update(config);

        logger.LogInformation(
            "Applied storage configuration v{Version}: {DatastoreCount} datastore(s), {EnvironmentCount} environment(s).",
            version, (config.Datastores ?? []).Length, (config.Environments ?? []).Length);

        // The datastore paths may have changed; restart the disk-store watcher so it inventories the new
        // locations (the same effect the interactive agent-settings sync has). Best-effort — the config
        // is already applied, so a watcher hiccup must not fail the apply.
        try
        {
            await diskStoresChangeWatcher.Restart();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart the disk-store watcher after a storage-config apply.");
        }
    }

    private async Task MergeIntoLocalCache(StorageConfig distributed)
    {
        // Merge the distributed paths over the local config, validate, and persist the result to
        // agentsettings.yml so it survives restarts and is picked up by GetCurrentConfiguration and all
        // path resolution. A read/validation/save failure must propagate: ConfigApplier turns a thrown
        // exception into a failed ConfigAppliedEvent so the controller retries — swallowing it would
        // leave the agent silently running the old paths while the controller believes it was applied.
        var hostSettings = await hostSettingsProvider.GetHostSettings()
            .Match(h => h, error => throw new InvalidOperationException(
                $"Failed to read the host settings: {error.Message}"));

        var local = await vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
            .Match(c => c, error => throw new InvalidOperationException(
                $"Failed to read agentsettings: {error.Message}"));

        var merged = StorageConfigMerge.Apply(local, distributed);
        Validate(merged).Match(_ => unit, error => throw new InvalidOperationException(error.Message));

        await vmHostAgentConfigurationManager.SaveConfiguration(merged, hostSettings)
            .Match(_ => unit, error => throw new InvalidOperationException(
                $"Failed to write the distributed storage configuration to agentsettings: {error.Message}"));

        WarnAboutUnusedLocalConfig(distributed, local);
        WarnAboutUnmappedDistributedDatastores(distributed, merged);
    }

    // Reuse the agent-settings validation (duplicate names/paths, well-formed paths) so the controller
    // path is not the least-validated writer of agentsettings.yml.
    private static Either<Error, Unit> Validate(VmHostAgentConfiguration config) =>
        VmHostAgentConfigurationValidations.ValidateVmHostAgentConfig(config)
            .ToEither()
            // Fold the individual issues into the message so surfacing it (Message) does not drop the
            // detail — the reason a merged config was rejected must reach the operator.
            .MapLeft(issues => Error.New(
                "The merged storage configuration is invalid: "
                + string.Join("; ", issues.Map(i => i.Message))));

    private void WarnAboutUnusedLocalConfig(StorageConfig distributed, VmHostAgentConfiguration local)
    {
        // Surface datastores/environments that are configured locally but not part of the distributed
        // vocabulary. The agent does not reject them, but the controller will never place on them. Uses
        // the pre-merge local config (what was genuinely local before this push), by design.
        foreach (var dataStore in StorageConfigValidation.GetUnusedLocalDatastores(distributed, local))
            logger.LogWarning(
                "Local datastore '{DataStore}' is configured in agentsettings but is not part of the controller "
                + "storage configuration; catlets cannot be placed on it.", dataStore);

        foreach (var environment in StorageConfigValidation.GetUnusedLocalEnvironments(distributed, local))
            logger.LogWarning(
                "Local environment '{Environment}' is configured in agentsettings but is not part of the controller "
                + "storage configuration; catlets cannot be placed in it.", environment);
    }

    private void WarnAboutUnmappedDistributedDatastores(
        StorageConfig distributed, VmHostAgentConfiguration merged)
    {
        // The inverse warning: a datastore is in the distributed vocabulary (so placement considers it
        // allowed) but has no path on this host even after the merge, so it would only fail opaquely at
        // VM-create time. Flag it here instead.
        var mappedWithPath = (merged.Datastores ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.Path))
            .Select(d => d.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var datastore in distributed.Datastores ?? [])
            if (!mappedWithPath.Contains(datastore.Name))
                logger.LogWarning(
                    "Distributed datastore '{DataStore}' has no local path on this host; catlets cannot be placed "
                    + "on it until a path is configured for it.", datastore.Name);
    }
}
