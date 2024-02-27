using System;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ProjectListRequest : ListRequest
{
    [FromRoute(Name = "projectId")] public override Guid Project { get; set; }

}