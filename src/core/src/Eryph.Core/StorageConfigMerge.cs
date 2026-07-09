using System;
using System.IO;
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
        VmHostAgentDataStoreConfiguration[]? local, StorageDatastoreConfig[]? distributed)
    {
        var distributedItems = distributed ?? [];
        var distributedNames = distributedItems
            .Select(d => d.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var distributedPaths = distributedItems
            .Select(d => d.Path)
            .OfType<string>()
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = (local ?? [])
            // Drop a stale local-only datastore whose path was reassigned to a distributed name (e.g. a
            // rename keeping the same path): keeping both would fail duplicate-path validation and wedge
            // the apply forever. The distributed entry is authoritative for that path.
            .Where(d => distributedNames.Contains(d.Name)
                        || string.IsNullOrWhiteSpace(d.Path)
                        || !distributedPaths.Contains(NormalizePath(d.Path)))
            .ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);

        foreach (var datastore in distributedItems)
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
        VmHostAgentEnvironmentConfiguration[]? local, StorageEnvironmentConfig[]? distributed)
    {
        var result = (local ?? [])
            .ToDictionary(e => e.Name, e => e, StringComparer.OrdinalIgnoreCase);

        foreach (var environment in distributed ?? [])
        {
            result.TryGetValue(environment.Name, out var existing);

            // A vocabulary-only environment (name, no paths) with no local counterpart must not be
            // materialized into agentsettings: StorageNames path resolution enumerates every
            // environment's defaults and throws on a null default path, so a path-less entry would break
            // inventory of ordinary default-store VMs host-wide. The name is still an allowed placement
            // target via the distributed vocabulary; it just needs a path (at a more specific scope)
            // before anything can be placed in it.
            if (existing is null && !ContributesPaths(environment))
                continue;

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

    private static bool ContributesPaths(StorageEnvironmentConfig environment) =>
        !string.IsNullOrWhiteSpace(environment.Defaults?.Vms)
        || !string.IsNullOrWhiteSpace(environment.Defaults?.Volumes)
        || (environment.Datastores ?? []).Any(d => !string.IsNullOrWhiteSpace(d.Path));

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(path).ToLowerInvariant();

    private static string? Coalesce(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}
