using System;

namespace Eryph.Core.VmAgent;

public class VmHostAgentEnvironmentConfiguration
{
    public string Name { get; init; } = string.Empty;

    public VmHostAgentDefaultsConfiguration Defaults { get; init; } = new();

    // Nullable to match the top-level VmHostAgentConfiguration.Datastores: this is a deserialized
    // (agentsettings.yml) contract, so an omitted section deserializes to null; consumers coalesce.
    public VmHostAgentDataStoreConfiguration[]? Datastores { get; init; } = [];
}
