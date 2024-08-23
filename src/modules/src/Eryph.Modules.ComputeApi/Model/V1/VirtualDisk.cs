using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class VirtualDisk
    {
        [Key] public required string Id { get; set; }

        public required string Name { get; set; }
        
        public required string Location { get; set; }
        
        public required string DataStore { get; set; }
        
        public required string Project { get; set; }
        
        public required string Environment { get; set; }

        public string Path { get; set; }

        public long SizeBytes { get; set; }

        public string ParentId { get; set; }

        public IEnumerable<CatletDrive> AttachedDrives { get; set; }
    }
}