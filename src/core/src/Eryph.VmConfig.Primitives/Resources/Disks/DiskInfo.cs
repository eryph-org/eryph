using System;

namespace Eryph.Resources.Disks
{
    public class DiskInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string StorageIdentifier { get; set; }
        public string DataStore { get; set; }
        public string Project { get; set; }
        public string Environment { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }

        public long? SizeBytes { get; set; }

        public DiskInfo Parent { get; set; }
    }
}