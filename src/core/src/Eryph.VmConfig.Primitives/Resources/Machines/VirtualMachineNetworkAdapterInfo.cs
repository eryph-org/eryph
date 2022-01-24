namespace Eryph.Resources.Machines
{
    public class VirtualMachineNetworkAdapterData
    {
        public string Id { get; set; }
        public string AdapterName { get; set; }
        public string VirtualSwitchName { get; set; }
        public ushort VLanId { get; set; }
        public string MACAddress { get; set; }
    }


    public class VMHostSwitchData
    {
        public string Id { get; set; }
        public string VirtualSwitchName { get; set; }
    }
}