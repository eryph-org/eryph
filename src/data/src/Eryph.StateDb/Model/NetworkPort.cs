using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public abstract class NetworkPort
{
    public string ProviderName { get; set; }

    public Guid Id { get; set; }
    public string MacAddress { get; set; }

    public string Name { get; set; }
    public virtual List<IpAssignment> IpAssignments { get; set; }

}