using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class ProjectRoles
{
    public Guid RoleId { get; set; }

    public Guid ProjectId { get; set; }

    public AccessRight AccessRight { get; set; }

    public virtual List<Project> Projects { get; set; }

}