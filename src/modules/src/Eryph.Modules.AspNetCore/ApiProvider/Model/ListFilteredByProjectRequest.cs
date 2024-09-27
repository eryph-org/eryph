using System;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ListFilteredByProjectRequest : ListRequest, IListFilteredByProjectRequest
{
    [FromQuery(Name = "projectId")] public string? ProjectId { get; set; }
}
