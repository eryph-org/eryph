using Haipa.VmConfig;

namespace Haipa.Messages.Events
{
    public class VirtualMachineDriveInfo
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