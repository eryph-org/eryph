namespace Eryph.StateDb.Model;

public abstract class Disk : Resource
{
    public required string DataStore { get; set; }

    public DiskType DiskType { get; set; }
}
