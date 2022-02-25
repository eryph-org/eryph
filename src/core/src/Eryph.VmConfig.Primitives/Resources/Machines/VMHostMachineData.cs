using Eryph.Core;

namespace Eryph.Resources.Machines
{
    public class VMHostMachineData : MachineData
    {
        public VMHostSwitchData[] Switches { get; set; }

        [PrivateIdentifier]
        public string HardwareId { get; set; }

        public HostVirtualNetworkData[] VirtualNetworks { get; set; }
}
}