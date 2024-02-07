using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ListByProjectRequest: IListRequest
{
    [FromQuery(Name = "count")] public bool Count { get; set; }
    [FromRoute(Name = "project")] public virtual string? Project { get; set; }

}