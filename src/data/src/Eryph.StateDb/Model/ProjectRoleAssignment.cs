using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class ProjectRoleAssignment
{
    public Guid Id { get; set; }

    public Project Project { get; set; } = null!;

    public Guid ProjectId { get; set; }

    public string IdentityId { get; set; } = "";

    public Guid RoleId { get; set; }
}
