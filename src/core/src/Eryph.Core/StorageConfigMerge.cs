using System;
using System.Collections.Generic;
using System.Linq;
using Eryph.Core.VmAgent;

namespace Eryph.Core;

/// <summary>
/// Merges the controller-distributed <see cref="StorageConfig"/> over an agent's local
/// <see cref="VmHostAgentConfiguration"/>: a distributed path wins where it is set, local values and
/// local-only settings (WatchFileSystem, OVN) are preserved otherwise. The result is written back to
/// <c>agentsettings.yml</c> as the local cache, so all downstream path resolution keeps going through
/// the single existing <see cref="VmHostAgentConfiguration"/> seam.
/// </summary>
public static class StorageConfigMerge
{
    public static VmHostAgentConfiguration Apply(
        VmHostAgentConfiguration local, StorageConfig distributed) =>
        new()
        {
            Defaults = MergeDefaults(local.Defaults, distributed.Defaults),
            Datastores = MergeDatastores(local.Datastores, distributed.Datastores),
            Environments = MergeEnvironments(local.Environments, distributed.Environments),
            Ovn = local.Ovn,
        };

    private static VmHostAgentDefaultsConfiguration MergeDefaults(
        VmHostAgentDefaultsConfiguration local, StorageDefaultsConfig? distributed) =>
        distributed is null
            ? local
            : new VmHostAgentDefaultsConfiguration
            {
                Vms = Coalesce(distributed.Vms, local.Vms),
                Volumes = Coalesce(distributed.Volumes, local.Volumes),
                WatchFileSystem = local.WatchFileSystem,
            };

    private static VmHostAgentDataStoreConfiguration[] MergeDatastores(
        VmHostAgentDataStoreConfiguration[]? local, StorageDatastoreConfig[] distributed)
    {
        var result = (local ?? [])
            .ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);

        foreach (var datastore in distributed)
        {
            // A distributed datastore without a path is vocabulary only; it cannot supply a path, so
            // leave any local mapping untouched (there is nothing to write for a path-less entry).
            if (string.IsNullOrWhiteSpace(datastore.Path))
                continue;

            result.TryGetValue(datastore.Name, out var existing);
            result[datastore.Name] = new VmHostAgentDataStoreConfiguration
            {
                Name = datastore.Name,
                Path = datastore.Path,
                WatchFileSystem = existing?.WatchFileSystem ?? true,
            };
        }

        return result.Values.ToArray();
    }

    private static VmHostAgentEnvironmentConfiguration[] MergeEnvironments(
        VmHostAgentEnvironmentConfiguration[]? local, StorageEnvironmentConfig[] distributed)
    {
        var result = (local ?? [])
            .ToDictionary(e => e.Name, e => e, StringComparer.OrdinalIgnoreCase);

        foreach (var environment in distributed)
        {
            result.TryGetValue(environment.Name, out var existing);
            result[environment.Name] = new VmHostAgentEnvironmentConfiguration
            {
                Name = environment.Name,
                Defaults = MergeDefaults(
                    existing?.Defaults ?? new VmHostAgentDefaultsConfiguration(), environment.Defaults),
                Datastores = MergeDatastores(existing?.Datastores, environment.Datastores),
            };
        }

        return result.Values.ToArray();
    }

    private static string? Coalesce(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}
