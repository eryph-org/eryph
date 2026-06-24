using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Applies the controller-distributed placement configuration: the datastore and
/// environment name catalog the agent is allowed to serve. The agent maps these
/// names to local paths itself (via <c>agentsettings.yml</c>); here it records the
/// received vocabulary so provisioning can enforce it, and warns about local
/// datastores/environments that the controller does not know — those can never be
/// used for placement.
/// </summary>
internal sealed class PlacementConfigRealizer(
    IPlacementConfigProvider placementConfigProvider,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    ILogger<PlacementConfigRealizer> logger)
    : IConfigRealizer
{
    public ConfigDomain Domain => ConfigDomain.PlacementConfig;

    public async Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        var config = JsonSerializer.Deserialize<PlacementConfig>(payload) ?? new PlacementConfig();
        placementConfigProvider.Update(config);

        logger.LogInformation(
            "Applied placement configuration v{Version}: {DatastoreCount} datastore(s), {EnvironmentCount} environment(s).",
            version, config.Datastores.Length, config.Environments.Length);

        await WarnAboutUnusedLocalConfig(config);
    }

    private async Task WarnAboutUnusedLocalConfig(PlacementConfig distributed)
    {
        // Best-effort: read the local agent settings to surface datastores/environments
        // that are configured locally but not part of the distributed vocabulary. The
        // agent does not reject them, but the controller will never place on them.
        var local = await hostSettingsProvider.GetHostSettings()
            .Bind(vmHostAgentConfigurationManager.GetCurrentConfiguration)
            .Match(c => c, _ => null);

        if (local is null)
            return;

        foreach (var dataStore in PlacementConfigValidation.GetUnusedLocalDatastores(distributed, local))
            logger.LogWarning(
                "Local datastore '{DataStore}' is configured in agentsettings but is not part of the controller "
                + "placement configuration; catlets cannot be placed on it.", dataStore);

        foreach (var environment in PlacementConfigValidation.GetUnusedLocalEnvironments(distributed, local))
            logger.LogWarning(
                "Local environment '{Environment}' is configured in agentsettings but is not part of the controller "
                + "placement configuration; catlets cannot be placed in it.", environment);
    }
}
