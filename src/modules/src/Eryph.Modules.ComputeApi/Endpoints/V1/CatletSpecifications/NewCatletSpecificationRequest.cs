using System;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class NewCatletSpecificationRequest : RequestBase
{
    public Guid? CorrelationId { get; set; }

    public required Guid ProjectId { get; set; }

    public required string Name { get; set; }

    public string? Comment { get; set; }

    public required string Configuration { get; set; }
}
