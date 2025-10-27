using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using System;
using System.Collections.Generic;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class ValidateSpecificationRequest : RequestBase
{
    public Guid? CorrelationId { get; set; }

    public required CatletSpecificationConfig Configuration { get; set; }

    public IList<string>? Architectures { get; set; }
}
