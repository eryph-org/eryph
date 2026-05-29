using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.ModuleCore.Components;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Advertises the host agent's locally-configured datastore and environment names
/// to the controller at registration. The names/paths are owned by the agent (the
/// local-config exception); reporting them up is for management transparency — so
/// admins can see each agent's settings centrally — and to support placement.
/// </summary>
internal sealed class HostAgentCapabilitiesProvider(
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    ILogger<HostAgentCapabilitiesProvider> logger)
    : IComponentCapabilitiesProvider
{
    public Task<IReadOnlyDictionary<string, string>> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
        (from hostSettings in hostSettingsProvider.GetHostSettings()
         from config in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
         select config)
        .Match(
            Right: BuildCapabilities,
            Left: error =>
            {
                logger.LogWarning(
                    "Could not read agent configuration to advertise capabilities: {Error}",
                    error.Message);
                return (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();
            });

    private static IReadOnlyDictionary<string, string> BuildCapabilities(VmHostAgentConfiguration config)
    {
        var datastores = (config.Datastores ?? [])
            .Select(d => d.Name)
            .Where(n => !string.IsNullOrEmpty(n));
        var environments = (config.Environments ?? [])
            .Select(e => e.Name)
            .Where(n => !string.IsNullOrEmpty(n));

        return new Dictionary<string, string>
        {
            ["datastores"] = string.Join(",", datastores),
            ["environments"] = string.Join(",", environments),
        };
    }
}
