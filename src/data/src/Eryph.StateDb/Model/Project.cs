using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class Project
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; }

    public virtual List<Resource> Resources { get; set; }

    public virtual List<ProjectRoles> Roles { get; set; }
}


public class ProjectRoles
{
    public Guid RoleId { get; set; }

    public Guid ProjectId { get; set; }

    public AccessRight AccessRight { get; set; }

    public virtual List<Project> Projects { get; set; }

}

public enum AccessRight
{
    None = 0,
    Read = 10,
    Write = 20,
    Admin = 99,
}