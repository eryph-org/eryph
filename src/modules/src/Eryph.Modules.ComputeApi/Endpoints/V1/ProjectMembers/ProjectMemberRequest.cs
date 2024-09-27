using System;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class ProjectMemberRequest : SingleEntityRequest
{
    [FromRoute(Name = "projectId")] public required string ProjectId { get; set; }
}
