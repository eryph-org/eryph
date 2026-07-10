namespace Eryph.Core;

/// <summary>
/// The controller-owned, operator-defined storage settings distributed to agents: the cluster
/// vocabulary of datastore and environment names together with the concrete filesystem paths they
/// map to. Paths are optional per entry — a scope may carry names only (vocabulary) while a more
/// specific scope (e.g. a host) supplies the machine-specific paths. An agent merges the distributed
/// values over its local <c>agentsettings.yml</c>, which becomes a cache of the distributed config.
/// </summary>
public sealed class StorageConfig
{
    /// <summary>Global default VM/volume base paths; null leaves the agent's local/host defaults.</summary>
    public StorageDefaultsConfig? Defaults { get; set; }

    // Nullable because this is a deserialized (YAML) contract: an omitted section or an explicit
    // `datastores: ~` deserializes the array to null, which the non-null annotation would hide. Consumers
    // coalesce with `?? []`. (A null list *item* is malformed input, caught as an invalid payload.)
    public StorageDatastoreConfig[]? Datastores { get; set; } = [];

    public StorageEnvironmentConfig[]? Environments { get; set; } = [];
}

/// <summary>
/// Base paths for VMs and volumes. The asymmetry with a datastore (which has a single path used for
/// both) is intentional and mirrors <c>VmHostAgentConfiguration</c>.
/// </summary>
public sealed class StorageDefaultsConfig
{
    public string? Vms { get; set; }

    public string? Volumes { get; set; }
}

/// <summary>A named datastore and, optionally, the local path it maps to.</summary>
public sealed class StorageDatastoreConfig
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The local path; null lists the datastore as vocabulary only (no path at this scope).</summary>
    public string? Path { get; set; }
}

/// <summary>A named environment with its own default paths and datastores.</summary>
public sealed class StorageEnvironmentConfig
{
    public string Name { get; set; } = string.Empty;

    public StorageDefaultsConfig? Defaults { get; set; }

    public StorageDatastoreConfig[]? Datastores { get; set; } = [];
}
