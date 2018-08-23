namespace HyperVPlus.Agent.Management.Data
{
    public abstract class DriveInfoBase : VirtualMachineDeviceInfo
    {

        public virtual string Path { get; set; }

        public string PoolName { get; set; }

    }
}