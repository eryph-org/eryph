namespace Eryph.StateDb.Model;

public class ProviderRouterPort : VirtualNetworkPort
{
    public string SubnetName { get; set; }
    public string PoolName { get; set; }

}