namespace Haipa.VmManagement.Data
{
    public interface IVMWithNetworkAdapterInfo<out T> where T : IVMNetworkAdapterCore
    {
        T[] NetworkAdapters { get; }
    }
}