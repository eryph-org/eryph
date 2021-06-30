using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Haipa.Resources.Machines;

namespace Haipa.Modules.ComputeApi.Model.V1
{
    public class VirtualMachine : Machine
    {
        public IEnumerable<VirtualMachineNetworkAdapter> NetworkAdapters { get; set; }

        public IEnumerable<VirtualMachineDrive> Drives { get; set; }
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

    }
}