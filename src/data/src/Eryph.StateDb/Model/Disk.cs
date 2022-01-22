namespace Eryph.StateDb.Model
{
    public class Disk : Resource
    {
        public string DataStore { get; set; }
        public string Project { get; set; }
        public string Environment { get; set; }

        public DiskType DiskType { get; set; }
    }
}