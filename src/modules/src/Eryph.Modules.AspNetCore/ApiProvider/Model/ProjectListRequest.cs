using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ProjectListRequest : ListRequest
{
    [FromRoute(Name = "project")] public override string? Project { get; set; }

}