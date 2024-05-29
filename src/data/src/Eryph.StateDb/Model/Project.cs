using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class Project
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public List<Resource> Resources { get; set; } = null!;

    public List<ProjectRoleAssignment> ProjectRoles { get; set; } = null!;

    public List<OperationTaskModel> ReferencedTasks { get; set; } = null!;
}
