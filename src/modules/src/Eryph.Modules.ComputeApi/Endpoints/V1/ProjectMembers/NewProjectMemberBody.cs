using System;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class NewProjectMemberBody
{
    public Guid? CorrelationId { get; set; }

    public string MemberId { get; set; }

    public Guid RoleId { get; set; }
}
