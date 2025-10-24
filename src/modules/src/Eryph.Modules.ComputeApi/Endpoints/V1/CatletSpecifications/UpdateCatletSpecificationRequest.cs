using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Mvc;
using System;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class UpdateCatletSpecificationRequest : SingleEntityRequest
{
    [FromBody] public required UpdateCatletSpecificationRequestBody Body { get; set; }
}

public class UpdateCatletSpecificationRequestBody
{
    public Guid? CorrelationId { get; set; }

    public string? Comment { get; set; }

    public required CatletSpecificationConfig Configuration { get; set; }
}
