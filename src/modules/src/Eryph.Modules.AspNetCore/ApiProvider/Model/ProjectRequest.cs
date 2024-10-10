using System;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ProjectRequest : RequestBase
{
    [FromRoute(Name = "project_id")] public required string ProjectId { get; set; }
}
