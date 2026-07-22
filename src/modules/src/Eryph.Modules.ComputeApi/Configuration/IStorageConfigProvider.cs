using Eryph.Core;

namespace Eryph.Modules.ComputeApi.Configuration;

/// <summary>
/// Holds the most recently applied controller-distributed <see cref="StorageConfig"/> — the datastore
/// name vocabulary — so the configuration-option endpoints can present it to clients. The compute API
/// is a read-only consumer of the config-distribution channel: it caches the distributed value here
/// (updated by <see cref="StorageConfigRealizer"/>) and never authors or realizes it.
/// </summary>
public interface IStorageConfigProvider
{
    /// <summary>The last applied storage configuration; an empty vocabulary until the first apply.</summary>
    StorageConfig Current { get; }

    void Update(StorageConfig config);
}

internal sealed class StorageConfigProvider : IStorageConfigProvider
{
    private volatile StorageConfig _current = new();

    public StorageConfig Current => _current;

    public void Update(StorageConfig config) => _current = config;
}
