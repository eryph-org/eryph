using System;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ListEntitiesRequest: IListEntitiesRequest
{
    [FromQuery(Name = "count")] public bool Count { get; set; }
}
