using System;

namespace Eryph.Core.VmAgent;

public class VmHostAgentConfiguration
{
    public VmHostAgentDefaultsConfiguration Defaults { get; init; } = new();

    public VmHostAgentDataStoreConfiguration[] Datastores { get; init; } = Array.Empty<VmHostAgentDataStoreConfiguration>();

    public VmHostAgentEnvironmentConfiguration[] Environments { get; init; } = Array.Empty<VmHostAgentEnvironmentConfiguration>();
}

