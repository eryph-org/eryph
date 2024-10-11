using System;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ListInProjectRequest : IListInProjectRequest
{
    [FromRoute(Name = "project_id")] public required string ProjectId { get; set; }
}
