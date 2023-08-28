namespace Eryph.Resources.Machines;


public sealed class MachineNetworkSettings
{
    public string AdapterName { get; set; }

    public string NetworkName { get; set; }

    public string NetworkProviderName { get; set; }
    public string PortName { get; set; }
    public string MacAddress { get; set; }
    public string AddressesV4 { get; set; }
    public string AddressesV6 { get; set; }
    public string FloatingAddressV4 { get; set; }
    public string FloatingAddressV6 { get; set; }

}