using System.Collections.Generic;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletSpecification
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required Project Project { get; set; }

    public required CatletSpecificationVersionInfo Latest { get; set; }

    /// <summary>
    /// The deployments of this specification, one per environment it is deployed into. Empty when it
    /// is not deployed anywhere.
    /// </summary>
    public IReadOnlyList<CatletSpecificationDeployment> Deployments { get; set; } = [];
}
