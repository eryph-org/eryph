using Haipa.Primitives.Resources.Disks;

namespace Haipa.Primitives.Resources.Machines
{
    public class VirtualMachineDriveData
    {
        public string Id { get; set; }

        public int ControllerLocation { get; set; }

        public int ControllerNumber { get; set; }

        public ControllerType ControllerType { get; set; }

        public VirtualMachineDriveType? Type { get; set; }

        public bool Frozen { get; set; }

        public DiskInfo Disk { get; set; }
    }
}