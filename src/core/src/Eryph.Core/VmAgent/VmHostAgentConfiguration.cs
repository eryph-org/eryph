namespace Eryph.Core.VmAgent;

public class VmHostAgentConfiguration
{
    public VmHostAgentDefaultsConfiguration Defaults { get; init; } = new();

    public VmHostAgentDataStoreConfiguration[]? Datastores { get; init; }

    public VmHostAgentEnvironmentConfiguration[]? Environments { get; init; }
}
