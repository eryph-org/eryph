using System;

namespace Eryph.StateDb.Model;

public class IpPoolAssignment : IpAssignment
{
    public Guid PoolId { get; set; }
    public int Number { get; set; }

    public virtual IpPool Pool { get; set; }


}