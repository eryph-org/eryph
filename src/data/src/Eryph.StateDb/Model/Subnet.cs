using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class Subnet
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string IpNetwork { get; set; }

    public virtual List<IpPool> IpPools { get; set; }
}