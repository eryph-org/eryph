using System.Threading.Tasks;

namespace Eryph.Modules.HostAgent.Inventory;

/// <summary>
/// Restarts the disk-store change watcher so it re-reads the configured datastore paths. Abstracted from
/// <see cref="DiskStoresChangeWatcherService"/> so consumers (e.g. the storage-config realizer) can
/// trigger a restart after the datastore paths change without depending on the hosted service directly.
/// </summary>
public interface IDiskStoresChangeWatcher
{
    Task Restart();
}
