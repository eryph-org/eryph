namespace Eryph.Modules.ComputeApi.Model.V1;

/// <summary>
/// A site of the deployment. Provided as an option list so clients can offer the available sites when
/// authoring configuration (e.g. which site realizes an environment).
/// </summary>
public class Site
{
    /// <summary>The site name.</summary>
    public required string Name { get; set; }
}
