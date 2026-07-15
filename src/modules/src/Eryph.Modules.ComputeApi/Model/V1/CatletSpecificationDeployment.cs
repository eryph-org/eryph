namespace Eryph.Modules.ComputeApi.Model.V1;

/// <summary>
/// A deployment of a catlet specification: the catlet it was deployed as, and the environment it was
/// deployed into. A specification is project level and can be deployed into several environments, at
/// most once into each.
/// </summary>
public class CatletSpecificationDeployment
{
    public required string Environment { get; set; }

    public required string CatletId { get; set; }
}
