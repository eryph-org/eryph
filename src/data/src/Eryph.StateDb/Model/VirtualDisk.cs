using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.StateDb.Model
{
    public class VirtualDisk : Disk
    {
        public VirtualDisk()
        {
            ResourceType = ResourceType.VirtualDisk;
        }

        public string StorageIdentifier { get; set; }

        public string Path { get; set; }
        public string FileName { get; set; }

        public long? SizeBytes { get; set; }

        public virtual VirtualDisk Parent { get; set; }
        public virtual ICollection<VirtualDisk> Childs { get; set; }
        public virtual ICollection<VirtualCatletDrive> AttachedDrives { get; set; }

        public Guid? ParentId { get; set; }
    }
}