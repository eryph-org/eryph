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
            result.TryGetValue(datastore.Name, out var existing);

            // A distributed datastore without a path is vocabulary only and keeps the local path; a
            // path-less entry with no local mapping has nothing to write, so skip it. Either way, adopt
            // the distributed (canonical) casing for the name — datastore path resolution downstream is
            // case-sensitive, so the local name must match how the controller addresses it.
            var path = string.IsNullOrWhiteSpace(datastore.Path) ? existing?.Path : datastore.Path;
            if (string.IsNullOrWhiteSpace(path))
                continue;

            result[datastore.Name] = new VmHostAgentDataStoreConfiguration
            {
                Name = datastore.Name,
                Path = path,
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
