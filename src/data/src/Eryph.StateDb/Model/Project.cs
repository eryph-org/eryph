using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class Project
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; }

    public virtual List<VirtualNetwork> VirtualNetworks { get; set; }
    public virtual List<Catlet> Catlets { get; set; }

}