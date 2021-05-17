using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Machines;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Query;

namespace Haipa.Modules.ComputeApi.Model.V1
{
    [Page(PageSize = 100)]
    [AutoExpand(DisableWhenSelectPresent = true)]
    public class VirtualMachine : Machine
    {
        [Contained] public IEnumerable<VirtualMachineNetworkAdapter> NetworkAdapters { get; set; }

        [Contained] public IEnumerable<VirtualMachineDrive> Drives { get; set; }

    }

    public class VirtualMachineNetworkAdapter
    {
        [Key] public string Id { get; set; }

        public string Name { get; set; }

        public string SwitchName { get; set; }

        public string MacAddress { get; set; }

    }

    public class VirtualMachineDrive
    {
        [Key] public string Id { get; set; }
        
        public VirtualMachineDriveType? Type { get; set; }

        public Guid AttachedDiskId { get; set; }

        public VirtualDisk AttachedDisk { get; set; }
    }
}
