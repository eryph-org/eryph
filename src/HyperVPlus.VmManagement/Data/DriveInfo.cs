namespace HyperVPlus.VmManagement.Data
{
    public abstract class DriveInfo : DriveInfoBase
    {

        public int ControllerLocation { get; set; }

        public int ControllerNumber { get; set; }

        public ControllerType ControllerType { get; set; }


    }

    public class VhdInfo
    {
        public string Path { get; set; }
        public long Size { get; set; }
    }
}