namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

/// <summary>
/// Contains versioning information for the REST API.
/// </summary>
public class ApiVersionResponse
{
    /// <summary>
    /// The latest version of the API supported by this instance.
    /// </summary>
    public required ApiVersion LatestVersion { get; init; } = new()
    {
        Major = 1,
        Minor = 3,
    };
}
