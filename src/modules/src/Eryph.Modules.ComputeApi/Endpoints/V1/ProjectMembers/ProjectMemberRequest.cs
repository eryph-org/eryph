using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class ProjectMemberRequest : SingleEntityRequest
{
    [FromRoute(Name = "projectId")] public virtual string Project { get; set; }

}