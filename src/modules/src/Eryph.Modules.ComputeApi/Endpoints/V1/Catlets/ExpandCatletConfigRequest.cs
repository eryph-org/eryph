using System;
using System.Text.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class ExpandCatletConfigRequest : SingleEntityRequest
{
    [FromBody] public required ExpandCatletConfigRequestBody Body { get; set; }
}

public class ExpandCatletConfigRequestBody
{
    public Guid? CorrelationId { get; set; }

    public required JsonElement Configuration { get; set; }

    public bool? ShowSecrets { get; set; }
}
