using System.Linq;
using Eryph.Core.VmAgent;

namespace Eryph.Core;

/// <summary>
/// Maps a <see cref="VmHostAgentConfiguration"/> to the distributable <see cref="StorageConfig"/> (its
/// storage subset — datastore/environment names and paths). Used where the local agent settings are the
/// source of the distributed storage config (eryph-zero: <c>agentsettings.yml</c> stays authoritative
/// for both the agent and the gene pool). Agent-local-only settings (WatchFileSystem, OVN) are dropped.
/// </summary>
public static class StorageConfigMapper
{
    public static StorageConfig FromVmHostAgentConfiguration(VmHostAgentConfiguration config) =>
        new()
        {
            Defaults = MapDefaults(config.Defaults),
            Datastores = (config.Datastores ?? [])
                .Select(d => new StorageDatastoreConfig { Name = d.Name, Path = d.Path })
                .ToArray(),
            Environments = (config.Environments ?? [])
                .Select(e => new StorageEnvironmentConfig
                {
                    Name = e.Name,
                    Defaults = MapDefaults(e.Defaults),
                    Datastores = e.Datastores
                        .Select(d => new StorageDatastoreConfig { Name = d.Name, Path = d.Path })
                        .ToArray(),
                })
                .ToArray(),
        };

    // A defaults block with no paths carries nothing distributable, so map it to null (omitted).
    private static StorageDefaultsConfig? MapDefaults(VmHostAgentDefaultsConfiguration defaults) =>
        string.IsNullOrWhiteSpace(defaults.Vms) && string.IsNullOrWhiteSpace(defaults.Volumes)
            ? null
            : new StorageDefaultsConfig { Vms = defaults.Vms, Volumes = defaults.Volumes };
}
