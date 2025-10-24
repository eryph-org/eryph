using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using System;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class ValidateSpecificationRequest : RequestBase
{
    public Guid? CorrelationId { get; set; }

    public required CatletSpecificationConfig Configuration { get; set; }

    public required string Architecture { get; set; }
}
