using System;
using Eryph.ConfigModel.Machine;

namespace Eryph.StateDb.Model
{
    public class VirtualMachineDrive
    {
        public string Id { get; set; }

        public Guid MachineId { get; set; }
        public virtual VirtualCatlet Vm { get; set; }

        public VirtualMachineDriveType? Type { get; set; }

        public Guid? AttachedDiskId { get; set; }

        public virtual VirtualDisk AttachedDisk { get; set; }
    }
}