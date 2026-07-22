using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.ComputeApi.Configuration;

/// <summary>
/// Applies the controller-distributed storage configuration for the compute API. Unlike the host agent,
/// the API has no local settings to merge into and nothing to realize against system state — it only
/// caches the distributed vocabulary in memory so the datastore option endpoint can serve it. Idempotent
/// by construction: applying any version just replaces the cached value.
/// </summary>
internal sealed class StorageConfigRealizer(
    IStorageConfigProvider storageConfigProvider,
    ILogger<StorageConfigRealizer> logger)
    : IConfigRealizer
{
    public ConfigDomain Domain => ConfigDomain.StorageConfig;

    public Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
    {
        var config = StorageConfigYamlSerializer.Deserialize(payload);
        storageConfigProvider.Update(config);

        logger.LogInformation(
            "Applied storage configuration v{Version}: {DatastoreCount} datastore(s).",
            version, (config.Datastores ?? []).Length);

        return Task.CompletedTask;
    }
}
