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

        public CatletDriveType? Type { get; set; }

        public bool Frozen { get; set; }

        /// <summary>
        /// Contains the actual virtual disk which is attached.
        /// </summary>
        /// <remarks>
        /// Can be <see langword="null"/> when e.g. the VHD has
        /// been deleted but is still attached to a VM in Hyper-V.
        /// </remarks>
        public DiskInfo Disk { get; set; }
    }
}