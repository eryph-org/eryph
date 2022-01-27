namespace Eryph.Resources.Machines
{
    public class VMHostMachineData : MachineData
    {
        public VMHostSwitchData[] Switches { get; set; }

        public string HardwareId { get; set; }

        public HostVirtualNetworkData[] VirtualNetworks { get; set; }
}
}