namespace Eryph.StateDb.Model;

public class FloatingNetworkPort : NetworkPort
{
    public VirtualNetworkPort AssignedPort { get; set; }

    public string SubnetName { get; set; }
    public string PoolName { get; set; }

}