using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

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
    IStorageConfigProvider placementConfigProvider,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    ILogger<StorageConfigRealizer> logger)
    : IConfigRealizer
{
    public ConfigDomain Domain => ConfigDomain.StorageConfig;

    public async Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        var config = StorageConfigYamlSerializer.Deserialize(payload);
        placementConfigProvider.Update(config);

        logger.LogInformation(
            "Applied storage configuration v{Version}: {DatastoreCount} datastore(s), {EnvironmentCount} environment(s).",
            version, config.Datastores.Length, config.Environments.Length);

        await MergeIntoLocalCache(config);
    }

    private async Task MergeIntoLocalCache(StorageConfig distributed)
    {
        // Merge the distributed paths over the local config and persist the result to agentsettings.yml,
        // so it survives restarts and is picked up by GetCurrentConfiguration and all path resolution.
        var local = await (
            from hostSettings in hostSettingsProvider.GetHostSettings()
            from current in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
            from _ in vmHostAgentConfigurationManager.SaveConfiguration(
                StorageConfigMerge.Apply(current, distributed), hostSettings)
            select current
        ).Match(
            Right: current => (VmHostAgentConfiguration?)current,
            Left: error =>
            {
                logger.LogWarning(
                    "Could not write the distributed storage configuration to agentsettings: {Error}.",
                    error.Message);
                return null;
            });

        if (local is not null)
            WarnAboutUnusedLocalConfig(distributed, local);
    }

    private void WarnAboutUnusedLocalConfig(StorageConfig distributed, VmHostAgentConfiguration local)
    {
        // Surface datastores/environments that are configured locally but not part of the distributed
        // vocabulary. The agent does not reject them, but the controller will never place on them.
        foreach (var dataStore in StorageConfigValidation.GetUnusedLocalDatastores(distributed, local))
            logger.LogWarning(
                "Local datastore '{DataStore}' is configured in agentsettings but is not part of the controller "
                + "placement configuration; catlets cannot be placed on it.", dataStore);

        foreach (var environment in StorageConfigValidation.GetUnusedLocalEnvironments(distributed, local))
            logger.LogWarning(
                "Local environment '{Environment}' is configured in agentsettings but is not part of the controller "
                + "placement configuration; catlets cannot be placed in it.", environment);
    }
}
