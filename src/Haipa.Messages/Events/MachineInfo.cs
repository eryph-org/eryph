using System;

namespace Haipa.Messages.Events
{
    public class MachineInfo
    {

        public string Name { get; set; }

        public Guid MachineId { get; set; }
        
        public VmStatus Status { get; set; }

        public VirtualMachineNetworkAdapterInfo[] NetworkAdapters { get; set; }
        public VirtualMachineNetworkInfo[] Networks { get; set; }
        public VirtualMachineDriveInfo[] Drives { get; set; }


    }
}