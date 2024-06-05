using System;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class NewProjectMemberBody
{
    public Guid? CorrelationId { get; set; }

    public required string MemberId { get; set; }

    public required Guid RoleId { get; set; }
}
