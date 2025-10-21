using System;
using System.Text.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class UpdateCatletSpecificationRequest : SingleEntityRequest
{
    [FromBody] public required UpdateCatletSpecificationRequestBody Body { get; set; }
}

public class UpdateCatletSpecificationRequestBody
{
    public Guid? CorrelationId { get; set; }

    public string? Comment { get; set; }

    public string? Name { get; set; }

    public required string Configuration { get; set; }
}
