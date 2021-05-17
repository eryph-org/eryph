namespace Haipa.VmManagement.Data.Core
{
    public abstract class DriveInfoBase : VirtualMachineDeviceInfo
    {
        public virtual string Path { get; set; }

        public string PoolName { get; set; }
    }
}