using System;
using System.ComponentModel.DataAnnotations;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.ProjectMembers;

public class NewProjectMemberRequest : RequestBase
{
    [FromBody] [Required] public NewProjectMemberBody Body { get; set; }
    [FromRoute(Name = "projectId")][Required] public Guid? ProjectId { get; set; }

}