using System;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model
{
    public class ResourceOperationRequest
    {
        [FromRoute(Name = "id")] public Guid ResourceId { get; set; }
    }
}