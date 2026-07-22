namespace Eryph.Modules.ComputeApi.Model.V1;

/// <summary>
/// An environment of the deployment together with the site which realizes it. Provided as an option
/// list so clients can offer the available environments when authoring configuration.
/// </summary>
public class Environment
{
    /// <summary>The environment name.</summary>
    public required string Name { get; set; }

    /// <summary>The name of the site which realizes this environment.</summary>
    public required string Site { get; set; }
}
