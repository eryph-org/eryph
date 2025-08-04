using Eryph.VmManagement.Data.Core;

namespace Eryph.VmManagement.Data
{
    public interface IVMWithDrivesInfo
    {
        VirtualMachineDeviceInfo[] DVDDrives { get; }
        VirtualMachineDeviceInfo[] HardDrives { get; }
    }
}