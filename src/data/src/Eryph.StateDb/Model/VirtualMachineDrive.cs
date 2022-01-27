using System;
using Eryph.Resources.Machines;

namespace Eryph.StateDb.Model
{
    public class VirtualMachineDrive
    {
        public string Id { get; set; }

        public Guid MachineId { get; set; }
        public virtual VirtualMachine Vm { get; set; }

        public VirtualMachineDriveType? Type { get; set; }

        public Guid? AttachedDiskId { get; set; }

        public virtual VirtualDisk AttachedDisk { get; set; }
    }
}