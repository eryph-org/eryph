namespace Eryph.VmManagement.Networking.Settings;

public class NetworkProviderSubnet
{
    public string Name { get; set; }
    public string Network { get; set; }
    public string Gateway { get; set; }

    public NetworkProviderIpPool[] IpPools { get; set; }
}