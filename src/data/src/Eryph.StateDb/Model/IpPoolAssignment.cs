using System;

namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class IpPoolAssignment : IpAssignment
{
    public Guid PoolId { get; set; }

    public IpPool Pool { get; set; } = null!;

    public int Number { get; set; }
}
