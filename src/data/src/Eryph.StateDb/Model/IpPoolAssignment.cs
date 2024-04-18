using System;

namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class IpPoolAssignment : IpAssignment
{
    public Guid PoolId { get; set; }
    public int Number { get; set; }

    public virtual IpPool Pool { get; set; }


}