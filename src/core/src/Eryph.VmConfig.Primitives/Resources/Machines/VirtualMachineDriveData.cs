using Eryph.ConfigModel.Catlets;
using Eryph.Resources.Disks;

namespace Eryph.Resources.Machines
{
    public class VirtualMachineDriveData
    {
        public string Id { get; set; }

        public int ControllerLocation { get; set; }

        public int ControllerNumber { get; set; }

        public ControllerType ControllerType { get; set; }

        public VirtualCatletDriveType? Type { get; set; }

        public bool Frozen { get; set; }

        public DiskInfo Disk { get; set; }
    }
}