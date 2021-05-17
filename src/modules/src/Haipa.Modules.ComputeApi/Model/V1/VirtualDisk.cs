using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Query;

namespace Haipa.Modules.ComputeApi.Model.V1
{
    [Page(PageSize = 100)]
    [AutoExpand(DisableWhenSelectPresent = true)]
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

        public Guid ParentId { get; set; }


        [Contained] public IEnumerable<VirtualMachineDrive> AttachedDrives { get; set; }
    }
}