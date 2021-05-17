using System;

namespace Haipa.VmManagement.Data
{
    public interface IVirtualMachineCoreInfo
    {
        Guid Id { get; }
        string Name { get; }

        //bool IsClustered { get; }
        string Path { get; }
        long MemoryStartup { get; }
        long ProcessorCount { get; }
        int Generation { get; }
    }
}