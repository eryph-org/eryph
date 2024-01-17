using System;
using JetBrains.Annotations;

namespace Eryph.Core.VmAgent;

public class VmHostAgentConfiguration
{
    public VmHostAgentDefaultsConfiguration Defaults { get; init; } = new();

    [CanBeNull] public VmHostAgentDataStoreConfiguration[] Datastores { get; init; }

    [CanBeNull] public VmHostAgentEnvironmentConfiguration[] Environments { get; init; }
}

