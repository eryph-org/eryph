using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class VirtualDisk
    {
        [Key] public Guid Id { get; set; }

        public string Name { get; set; }
        public string StorageIdentifier { get; set; }
        public string DataStore { get; set; }
        public string Project { get; set; }
        public string Environment { get; set; }

        public string Path { get; set; }


        public long? SizeBytes { get; set; }

        public Guid? ParentId { get; set; }


        public IEnumerable<CatletDrive> AttachedDrives { get; set; }
    }
}