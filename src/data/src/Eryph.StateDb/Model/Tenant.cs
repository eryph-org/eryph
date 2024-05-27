using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class Tenant
{
    public Guid Id { get; set; }

    public virtual List<Project> Projects { get; set; } = null!;
}