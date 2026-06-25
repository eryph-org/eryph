using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletSpecification
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required Project Project { get; set; }

    public required CatletSpecificationVersionInfo Latest { get; set; }

    public string? CatletId { get; set; }
}
