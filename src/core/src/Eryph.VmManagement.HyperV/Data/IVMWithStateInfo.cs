using Eryph.VmManagement.Data.enums;

namespace Eryph.VmManagement.Data;

public interface IVMWithStateInfo
{
    VirtualMachineState State { get; }
}
