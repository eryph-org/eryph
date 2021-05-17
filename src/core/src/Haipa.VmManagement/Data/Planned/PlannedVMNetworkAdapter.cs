using Haipa.VmManagement.Data.Core;

namespace Haipa.VmManagement.Data.Planned
{
    public class PlannedVMNetworkAdapter : VirtualMachineDeviceInfo, IVMNetworkAdapterCore
    {
        public bool DynamicMacAddressEnabled { get; private set; }
        public string MacAddress { get; private set; }
    }
}