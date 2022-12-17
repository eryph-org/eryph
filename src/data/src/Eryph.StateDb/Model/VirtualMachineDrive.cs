using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.StateDb.Model
{
    public class VirtualCatletDrive
    {
        public string Id { get; set; }

        public Guid MachineId { get; set; }
        public virtual VirtualCatlet Vm { get; set; }

        public VirtualCatletDriveType? Type { get; set; }

        public Guid? AttachedDiskId { get; set; }

        public virtual VirtualDisk AttachedDisk { get; set; }
    }
}