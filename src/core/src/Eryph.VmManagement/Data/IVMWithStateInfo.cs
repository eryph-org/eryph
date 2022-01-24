namespace Eryph.VmManagement.Data
{
    public interface IVMWithStateInfo
    {
        VirtualMachineState State { get; }
    }
}