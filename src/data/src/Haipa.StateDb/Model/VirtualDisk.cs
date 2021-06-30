using System;
using System.Collections.Generic;

namespace Haipa.StateDb.Model
{
    public class VirtualDisk : Disk
    {
        public string StorageIdentifier { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }

        public long? SizeBytes { get; set; }

        public VirtualDisk Parent { get; set; }
        public List<VirtualDisk> Childs { get; set; }
        public List<VirtualMachineDrive> AttachedDrives { get; set; }

        public Guid ParentId { get; set; }
    }
}