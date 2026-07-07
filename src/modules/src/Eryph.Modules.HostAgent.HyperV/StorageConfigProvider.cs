using Eryph.Core;

namespace Eryph.Modules.HostAgent;

/// <summary>
/// Holds the most recently applied controller-distributed <see cref="StorageConfig"/>
/// — the datastore/environment name vocabulary the agent is allowed to serve — so the
/// provisioning handlers can enforce it. Updated by <see cref="StorageConfigRealizer"/>.
/// </summary>
internal interface IStorageConfigProvider
{
    /// <summary>The last applied placement configuration; empty until the first apply.</summary>
    StorageConfig Current { get; }

    void Update(StorageConfig config);
}

internal sealed class StorageConfigProvider : IStorageConfigProvider
{
    private volatile StorageConfig _current = new();

    public StorageConfig Current => _current;

    public void Update(StorageConfig config) => _current = config ?? new StorageConfig();
}
