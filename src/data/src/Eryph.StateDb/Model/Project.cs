using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model;

// The change tracking in the controller module must be updated when modifying this entity.
public class Project
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public Guid TenantId { get; set; }

    public virtual Tenant Tenant { get; set; }

    public virtual List<Resource> Resources { get; set; }

    public virtual List<ProjectRoleAssignment> ProjectRoles { get; set; }

    public virtual List<OperationTaskModel> ReferencedTasks { get; set; }
}