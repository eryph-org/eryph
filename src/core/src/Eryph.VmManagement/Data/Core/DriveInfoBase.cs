using Eryph.ConfigModel;
using Eryph.Core;

namespace Eryph.VmManagement.Data.Core
{
    public abstract class DriveInfoBase : VirtualMachineDeviceInfo
    {
        [PrivateIdentifier]
        public virtual string Path { get; set; }

        public string PoolName { get; set; }
    }
}