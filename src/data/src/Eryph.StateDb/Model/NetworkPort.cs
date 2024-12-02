using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public abstract class NetworkPort
{
    public string? ProviderName { get; set; }

    public Guid Id { get; set; }
    
    public required string MacAddress { get; set; }

    public string? AddressName { get; set; }

    public required string Name { get; set; }

    public List<IpAssignment> IpAssignments { get; set; } = null!;
}
