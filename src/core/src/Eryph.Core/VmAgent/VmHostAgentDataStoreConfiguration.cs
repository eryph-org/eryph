namespace Eryph.Core.VmAgent
{
    public class VmHostAgentDataStoreConfiguration
    {
        public string Name { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;

        public bool WatchFileSystem { get; init; } = true;
    }
}
