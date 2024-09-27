using System;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ListEntitiesFilteredByProjectRequest
    : ListEntitiesRequest, IListEntitiesFilteredByProjectRequest
{
    [FromQuery(Name = "projectId")] public string? ProjectId { get; set; }
}
