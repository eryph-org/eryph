using System;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class ValidateSpecificationRequest : RequestBase
{
    public Guid? CorrelationId { get; set; }

    public required string Configuration { get; set; }
}
