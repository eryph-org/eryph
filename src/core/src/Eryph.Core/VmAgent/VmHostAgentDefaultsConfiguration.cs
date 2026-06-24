namespace Eryph.Core.VmAgent;

public class VmHostAgentDefaultsConfiguration
{
    public string? Vms { get; init; }

    public string? Volumes { get; init; }

    public bool WatchFileSystem { get; init; } = true;
}
