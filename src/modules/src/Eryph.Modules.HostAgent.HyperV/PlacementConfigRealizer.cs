using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Applies the controller-distributed placement configuration: the datastore and
/// environment name catalog the agent is allowed to serve. The agent maps these
/// names to local paths itself; here it just records the received vocabulary.
/// </summary>
internal sealed class PlacementConfigRealizer(
    ILogger<PlacementConfigRealizer> logger)
    : IConfigRealizer
{
    public ConfigDomain Domain => ConfigDomain.PlacementConfig;

    public Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        var config = JsonSerializer.Deserialize<PlacementConfig>(payload) ?? new PlacementConfig();
        logger.LogInformation(
            "Applied placement configuration v{Version}: {DatastoreCount} datastore(s), {EnvironmentCount} environment(s).",
            version, config.Datastores.Length, config.Environments.Length);
        return Task.CompletedTask;
    }
}
