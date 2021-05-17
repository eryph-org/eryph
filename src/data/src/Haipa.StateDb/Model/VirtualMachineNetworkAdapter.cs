using System;
using System.Collections.Generic;
using Haipa.VmConfig;

namespace Haipa.StateDb.Model
{
    public class VirtualMachineNetworkAdapter
    {
        public string Id { get; set; }

        public long MachineId { get; set; }
        public VirtualMachine Vm { get; set; }
        public string Name { get; set; }

        public string SwitchName { get; set; }

        public string MacAddress { get; set; }

    }

    public class VirtualMachineDrive
   {
        public string Id { get; set; }

        public long MachineId { get; set; }
        public VirtualMachine Vm { get; set; }

        public VirtualMachineDriveType? Type { get; set; }

        public Guid AttachedDiskId { get; set; }

        public VirtualDisk AttachedDisk { get; set; }
    }

    public class VirtualDisk
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

        public VirtualDisk Parent { get; set; }
        public List<VirtualDisk> Childs { get; set; }
        public List<VirtualMachineDrive> AttachedDrives { get; set; }

        public Guid ParentId { get; set; }


    }
}