using System;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.AspNetCore.ApiProvider.Model
{
    public class ResourceOperationRequest
    {
        [FromRoute(Name = "id")] public Guid ResourceId { get; set; }
    }
}