using System;

namespace Haipa.Messages.Events
{
    public class VirtualMachineInfo : MachineInfo
    {
        public Guid VMId { get; set; }

        public Guid MetadataId { get; set; }


        public VmStatus Status { get; set; }

        public VirtualMachineNetworkAdapterInfo[] NetworkAdapters { get; set; }
        public VirtualMachineDriveInfo[] Drives { get; set; }


    }

    public class VMHostMachineInfo : MachineInfo
    {
        public VMHostSwitchInfo[] Switches { get; set; }

    }


    public class MachineInfo
    {

        public string Name { get; set; }

        public MachineNetworkInfo[] Networks { get; set; }

    }
}