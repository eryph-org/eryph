using System;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ListInProjectRequest : IListRequest
{
    [FromRoute(Name = "projectId")] public required string ProjectId { get; set; }
}
