﻿using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public abstract class Subnet
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? IpNetwork { get; set; }

    public string? DnsDomain { get; set; }

    public List<IpPool> IpPools { get; set; } = null!;

    public List<IpAssignment> IpAssignments { get; set; } = null!;
}