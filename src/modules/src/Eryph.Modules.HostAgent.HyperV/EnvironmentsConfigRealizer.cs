using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Applies the controller-distributed environment catalog: the environment vocabulary the agent may
/// serve. Unlike the storage configuration there is nothing to merge into
/// <c>agentsettings.yml</c> — an environment definition has no agent-local half, as the paths an
/// environment maps to are the storage configuration's concern. Local environments the controller
/// does not know are warned about: they can never be placed in.
/// </summary>
internal sealed class EnvironmentsConfigRealizer(
    IEnvironmentsConfigProvider environmentsConfigProvider,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    ILogger<EnvironmentsConfigRealizer> logger)
    : IConfigRealizer
{
    public ConfigDomain Domain => ConfigDomain.Environments;

    public async Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        var config = EnvironmentsConfigYamlSerializer.Deserialize(payload);

        environmentsConfigProvider.Update(config);

        logger.LogInformation(
            "Applied environment configuration v{Version}: {EnvironmentCount} environment(s).",
            version, (config.Environments ?? []).Length);

        await WarnAboutUnusedLocalEnvironments(config);
    }

    private async Task WarnAboutUnusedLocalEnvironments(EnvironmentsConfig distributed)
    {
        // Best-effort: the vocabulary is already applied, so failing to read the local settings must
        // not fail the apply. It only costs the warning.
        var local = await hostSettingsProvider.GetHostSettings()
            .Bind(vmHostAgentConfigurationManager.GetCurrentConfiguration)
            .Match(c => c, error =>
            {
                logger.LogDebug(
                    "Could not read agentsettings to check for unused local environments: {Error}.",
                    error.Message);
                return null;
            });

        if (local is null)
            return;

        foreach (var environment in EnvironmentsConfigValidation.GetUnusedLocalEnvironments(distributed, local))
            logger.LogWarning(
                "Local environment '{Environment}' is configured in agentsettings but is not part of the "
                + "controller environment configuration; catlets cannot be placed in it.", environment);
    }
}
