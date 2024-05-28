using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class IpPool
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? FirstIp { get; set; }

    public string? NextIp { get; set; }
    
    public string? LastIp { get; set; }

    public string? IpNetwork { get; set; }

    public Guid SubnetId { get; set; }

    public Subnet Subnet { get; set; } = null!;

    public List<IpPoolAssignment> IpAssignments { get; set; } = null!;
}