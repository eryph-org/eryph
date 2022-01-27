using Eryph.VmManagement.Data.Core;

namespace Eryph.VmManagement.Data
{
    public interface IVMWithNetworkAdapterInfo
    {
        VirtualMachineDeviceInfo[] NetworkAdapters { get; }
    }
}