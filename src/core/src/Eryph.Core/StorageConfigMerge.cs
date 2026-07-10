using System;
using System.Collections.Generic;
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
/// <remarks>
/// A local entry whose path was re-assigned by the distributed config (a datastore/environment rename,
/// or a path moved between levels) is DROPPED: the agent validates the merged result with a
/// <b>global</b> no-duplicate-paths rule, so keeping the stale local entry alongside the distributed one
/// would fail validation and wedge the apply loop forever. Distributed paths are collected once across
/// every level and any local path they claim is pruned regardless of where it sat locally.
/// </remarks>
public static class StorageConfigMerge
{
    public static VmHostAgentConfiguration Apply(
        VmHostAgentConfiguration local, StorageConfig distributed)
    {
        var claimed = CollectDistributedPaths(distributed);
        return new VmHostAgentConfiguration
        {
            Defaults = MergeDefaults(local.Defaults, distributed.Defaults, claimed),
            Datastores = MergeDatastores(local.Datastores, distributed.Datastores, claimed),
            Environments = MergeEnvironments(local.Environments, distributed.Environments, claimed),
            Ovn = local.Ovn,
        };
    }

    // Every normalized path the distributed config assigns, across all levels. A distributed config is
    // duplicate-free (enforced at authoring), so each path appears once; any LOCAL entry holding one of
    // these paths under a different identity is stale and must be pruned to keep the merged result valid.
    private static HashSet<string> CollectDistributedPaths(StorageConfig distributed)
    {
        var paths = new List<string?> { distributed.Defaults?.Vms, distributed.Defaults?.Volumes };
        foreach (var datastore in distributed.Datastores ?? [])
            paths.Add(datastore.Path);
        foreach (var environment in distributed.Environments ?? [])
        {
            paths.Add(environment.Defaults?.Vms);
            paths.Add(environment.Defaults?.Volumes);
            foreach (var datastore in environment.Datastores ?? [])
                paths.Add(datastore.Path);
        }

        return paths
            .OfType<string>()
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static VmHostAgentDefaultsConfiguration MergeDefaults(
        VmHostAgentDefaultsConfiguration local,
        StorageDefaultsConfig? distributed,
        HashSet<string> claimed) =>
        new()
        {
            Vms = ResolveDefaultPath(distributed?.Vms, local.Vms, claimed),
            Volumes = ResolveDefaultPath(distributed?.Volumes, local.Volumes, claimed),
            // Preserve the local-only watch flag.
            WatchFileSystem = local.WatchFileSystem,
        };

    // The distributed default wins when set; otherwise keep the local value UNLESS the distributed config
    // assigned that path to some other entry (which would duplicate it after the merge), in which case
    // drop it.
    private static string? ResolveDefaultPath(string? distributed, string? local, HashSet<string> claimed)
    {
        if (!string.IsNullOrWhiteSpace(distributed))
            return distributed;
        return !string.IsNullOrWhiteSpace(local) && !claimed.Contains(NormalizePath(local))
            ? local
            : null;
    }

    private static VmHostAgentDataStoreConfiguration[] MergeDatastores(
        VmHostAgentDataStoreConfiguration[]? local,
        StorageDatastoreConfig[]? distributed,
        HashSet<string> claimed)
    {
        var distributedItems = distributed ?? [];
        var distributedNames = distributedItems
            .Select(d => d.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Group-last by name so a hand-edited local file with case-duplicate names does not crash the
        // merge (ToDictionary would throw); the last entry wins, matching serializer behaviour.
        var result = (local ?? [])
            // Keep a local datastore unless the distributed config claimed its path under a different
            // identity (rename / cross-level move). A name that is also distributed is kept (overridden
            // below); a path-less local entry has nothing to collide.
            .Where(d => distributedNames.Contains(d.Name)
                        || string.IsNullOrWhiteSpace(d.Path)
                        || !claimed.Contains(NormalizePath(d.Path)))
            .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

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
        VmHostAgentEnvironmentConfiguration[]? local,
        StorageEnvironmentConfig[]? distributed,
        HashSet<string> claimed)
    {
        var result = (local ?? [])
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        foreach (var environment in distributed ?? [])
        {
            result.TryGetValue(environment.Name, out var existing);

            // A distributed environment with no local counterpart is only materialized if it supplies
            // BOTH default paths: the agent requires every environment to have default Vms and Volumes
            // paths, so an environment that carries only a name or only datastores cannot form a valid
            // environment on its own (materializing it would fail validation and wedge the apply). The
            // name is still an allowed placement target via the distributed vocabulary; it becomes usable
            // once a more specific scope (or the local config) supplies its default paths.
            if (existing is null && !ProvidesDefaultPaths(environment))
                continue;

            result[environment.Name] = new VmHostAgentEnvironmentConfiguration
            {
                Name = environment.Name,
                Defaults = MergeDefaults(
                    existing?.Defaults ?? new VmHostAgentDefaultsConfiguration(), environment.Defaults, claimed),
                Datastores = MergeDatastores(existing?.Datastores, environment.Datastores, claimed),
            };
        }

        // Prune paths from local-only environments (not present in the distributed config) that the
        // distributed config re-assigned elsewhere — e.g. an environment rename, where the old local
        // environment would otherwise keep a path the new one now owns and fail duplicate-path validation.
        var distributedNames = (distributed ?? [])
            .Select(e => e.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in result.Keys.ToArray())
        {
            if (distributedNames.Contains(name))
                continue;
            result[name] = PruneClaimedPaths(result[name], claimed);
        }

        return result.Values.ToArray();
    }

    // Drops the paths of a local-only environment that the distributed config claimed elsewhere, so the
    // stale environment cannot duplicate a path the merged result already uses. An environment left with
    // no paths is harmless (path resolution skips path-less candidates).
    private static VmHostAgentEnvironmentConfiguration PruneClaimedPaths(
        VmHostAgentEnvironmentConfiguration environment, HashSet<string> claimed) =>
        new()
        {
            Name = environment.Name,
            Defaults = new VmHostAgentDefaultsConfiguration
            {
                Vms = ClaimedToNull(environment.Defaults.Vms, claimed),
                Volumes = ClaimedToNull(environment.Defaults.Volumes, claimed),
                WatchFileSystem = environment.Defaults.WatchFileSystem,
            },
            Datastores = (environment.Datastores ?? [])
                .Where(d => string.IsNullOrWhiteSpace(d.Path) || !claimed.Contains(NormalizePath(d.Path)))
                .ToArray(),
        };

    private static string? ClaimedToNull(string? path, HashSet<string> claimed) =>
        !string.IsNullOrWhiteSpace(path) && claimed.Contains(NormalizePath(path)) ? null : path;

    private static bool ProvidesDefaultPaths(StorageEnvironmentConfig environment) =>
        !string.IsNullOrWhiteSpace(environment.Defaults?.Vms)
        && !string.IsNullOrWhiteSpace(environment.Defaults?.Volumes);

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(path).ToLowerInvariant();
}
