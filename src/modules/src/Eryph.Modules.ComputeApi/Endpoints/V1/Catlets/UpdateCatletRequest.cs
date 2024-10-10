using System;
using System.Text.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class UpdateCatletRequest : SingleEntityRequest
{
    [FromBody] public required UpdateCatletRequestBody Body { get; set; }
}

public class UpdateCatletRequestBody
{
    public Guid? CorrelationId { get; set; }

    public required JsonElement Configuration { get; set; }
}
