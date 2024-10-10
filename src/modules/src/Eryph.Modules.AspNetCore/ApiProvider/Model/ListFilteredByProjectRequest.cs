using System;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ListFilteredByProjectRequest : ListRequest, IListFilteredByProjectRequest
{
    [FromQuery(Name = "project_id")] public string? ProjectId { get; set; }
}
