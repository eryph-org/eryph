using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.AspNetCore.ApiProvider.Model
{
    public class SingleResourceRequest : RequestBase
    {
        [FromRoute(Name = "id")] public string? Id { get; set; }

    }
}