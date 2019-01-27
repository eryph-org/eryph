using System;

namespace Haipa.VmManagement.Data
{
    public class MinimizedVirtualMachineInfo
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public VirtualMachineState State { get; private set; }
        public MinimizedVMNetworkAdapter[] NetworkAdapters { get; private set; }

    }

    public class MinimizedVMNetworkAdapter
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string MacAddress { get; private set; }
        public string SwitchName { get; private set; }
        public VMNetworkAdapterVlanSetting VlanSetting { get; private set; }
    }
}