namespace Haipa.VmManagement.Data
{
    public interface IDriveInfo
    {
        int ControllerLocation { get; set; }
        int ControllerNumber { get; set; }
        ControllerType ControllerType { get; set; }
        string Path { get; set; }
    }
}