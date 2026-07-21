using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.ComputeApi.Configuration;

/// <summary>
/// Applies the controller-distributed environment catalog for the compute API. As with the storage
/// configuration there is nothing local to merge or realize — the API caches the distributed sites and
/// environments in memory so the environment/site option endpoints can serve them. Idempotent: applying
/// any version just replaces the cached value.
/// </summary>
internal sealed class EnvironmentsConfigRealizer(
    IEnvironmentsConfigProvider environmentsConfigProvider,
    ILogger<EnvironmentsConfigRealizer> logger)
    : IConfigRealizer
{
    public ConfigDomain Domain => ConfigDomain.Environments;

    public Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        var config = EnvironmentsConfigYamlSerializer.Deserialize(payload);
        environmentsConfigProvider.Update(config);

        logger.LogInformation(
            "Applied environment configuration v{Version}: {SiteCount} site(s), {EnvironmentCount} environment(s).",
            version, (config.Sites ?? []).Length, (config.Environments ?? []).Length);

        return Task.CompletedTask;
    }
}
