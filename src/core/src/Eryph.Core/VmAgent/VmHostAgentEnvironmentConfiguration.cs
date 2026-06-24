using System;

namespace Eryph.Core.VmAgent;

public class VmHostAgentEnvironmentConfiguration
{
    public string Name { get; init; } = string.Empty;

    public VmHostAgentDefaultsConfiguration Defaults { get; init; } = new();

    public VmHostAgentDataStoreConfiguration[] Datastores { get; init; } =
        [];
}
