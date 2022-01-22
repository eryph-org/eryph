using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model
{
    public class ListRequest
    {
        [FromQuery(Name = "count")] public bool Count { get; set; }
    }
}