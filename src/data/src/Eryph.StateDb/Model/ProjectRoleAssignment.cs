using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

public class ProjectRoleAssignment
{
    public Guid Id { get; set; }

    public Project Project { get; set; }
    public Guid ProjectId { get; set; }
    public string IdentityId { get; set; }

    public Guid RoleId { get; set; }


}