using System;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model
{
    public class ListRequest: IListRequest
    {
        [FromQuery(Name = "count")] public bool Count { get; set; }
        [FromQuery(Name = "project")] public virtual string? Project { get; set; }

    }
}