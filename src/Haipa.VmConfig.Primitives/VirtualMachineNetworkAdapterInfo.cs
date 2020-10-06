namespace Haipa.Messages.Events
{
    public class VirtualMachineNetworkAdapterInfo
    {
        public string AdapterName { get; set; }
        public string VirtualSwitchName { get; set; }
        public ushort VLanId { get; set; }
        public string MACAddress { get; set; }        
    }
}