using Haipa.VmManagement.Data.Core;

namespace Haipa.VmManagement.Data.Planned
{
    public class PlannedVMNetworkAdapter : VirtualMachineDeviceInfo, IVMNetworkAdapterCore
    {
        public string MacAddress { get; private set; }

        public bool DynamicMacAddressEnabled { get; private set; }

    }
}