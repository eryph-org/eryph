using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model
{
    public class SingleResourceRequest : RequestBase
    {
        [FromRoute(Name = "id")] public string? Id { get; set; }

    }
}