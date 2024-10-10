using System;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class NewProjectMemberRequest : ProjectRequest
{
    [FromBody] public required NewProjectMemberBody Body { get; set; }
}
