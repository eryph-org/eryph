namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class FloatingNetworkPort : NetworkPort
{
    public VirtualNetworkPort? AssignedPort { get; set; }

    public required string SubnetName { get; set; }

    public required string PoolName { get; set; }
}
