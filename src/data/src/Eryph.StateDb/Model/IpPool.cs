using System;
using System.Collections.Generic;
using System.Numerics;
using LanguageExt;

namespace Eryph.StateDb.Model;

public class IpPool
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string FirstIp { get; set; }
    public string LastIp { get; set; }

    public string IpNetwork { get; set; }

    public int Counter { get; set; }

    public byte[] RowVersion { get; set; }


    public Guid SubnetId { get; set; }
    public Subnet Subnet { get; set; }

    public virtual List<IpPoolAssignment> IpAssignments { get; set; }
}