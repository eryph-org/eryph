using Eryph.ConfigModel;

namespace Eryph.VmManagement.Data.Core
{
    public abstract class DriveInfoBase : VirtualMachineDeviceInfo
    {
        [PrivateIdentifier]
        public virtual string Path { get; set; }

        public string PoolName { get; set; }
    }
}