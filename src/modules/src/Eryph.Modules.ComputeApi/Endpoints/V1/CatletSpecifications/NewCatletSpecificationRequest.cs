using System;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class NewCatletSpecificationRequest : RequestBase
{
    public Guid? CorrelationId { get; set; }

    public required Guid ProjectId { get; set; }

    public string? Comment { get; set; }

    public required CatletSpecificationConfig Configuration { get; set; }
}
