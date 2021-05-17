namespace Haipa.VmManagement.Data
{
    public interface IVMWithStateInfo
    {
        VirtualMachineState State { get; }
    }
}