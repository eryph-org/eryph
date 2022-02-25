namespace Eryph.Runtime.Zero.Configuration.Storage;

internal class StorageConfig
{
    public string Id{ get; set; }
    public string StorageIdentifier { get; set; }
    public string DataStore { get; set; }
    public string Project { get; set; }
    public string Environment { get; set; }

    public StorageVhdConfig[] VirtualDisks { get; set; }
}