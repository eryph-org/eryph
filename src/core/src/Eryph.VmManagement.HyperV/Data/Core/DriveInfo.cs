using Eryph.Resources.Disks;

namespace Eryph.VmManagement.Data.Core
{
    public abstract class DriveInfo : DriveInfoBase
    {
        public int ControllerLocation { get; set; }

        public int ControllerNumber { get; set; }

        public ControllerType ControllerType { get; set; }
    }
}