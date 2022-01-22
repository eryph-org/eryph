using Eryph.VmManagement.Data.Core;

namespace Eryph.VmManagement.Data.Planned
{
    public class PlannedVMNetworkAdapter : VirtualMachineDeviceInfo, IVMNetworkAdapterCore
    {
        public bool DynamicMacAddressEnabled { get; private set; }
        public string MacAddress { get; private set; }
    }
}