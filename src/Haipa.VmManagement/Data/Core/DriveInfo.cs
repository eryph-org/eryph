using Haipa.Messages.Events;

namespace Haipa.VmManagement.Data.Core
{
    public abstract class DriveInfo : DriveInfoBase, IDriveInfo
    {

        public int ControllerLocation { get; set; }

        public int ControllerNumber { get; set; }

        public ControllerType ControllerType { get; set; }


    }
}