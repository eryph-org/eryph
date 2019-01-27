namespace Haipa.VmManagement.Data
{
    public abstract class DriveInfo : DriveInfoBase
    {

        public int ControllerLocation { get; set; }

        public int ControllerNumber { get; set; }

        public ControllerType ControllerType { get; set; }


    }
}