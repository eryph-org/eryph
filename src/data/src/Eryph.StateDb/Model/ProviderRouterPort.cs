namespace Eryph.StateDb.Model;

public class ProviderRouterPort : VirtualNetworkPort
{
    public required string SubnetName { get; set; }

    public required string PoolName { get; set; }
}
