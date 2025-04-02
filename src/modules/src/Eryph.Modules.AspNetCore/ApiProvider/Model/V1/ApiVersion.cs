namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

/// <summary>
/// Encodes an API version.
/// </summary>
public class ApiVersion
{
    /// <summary>
    /// The major version of the API. The major version is incremented when
    /// breaking changes are introduced to the API. The version part of the
    /// endpoint URLs will change in this case.
    /// </summary>
    public required int Major { get; init; }

    /// <summary>
    /// The minor version of the API. The minor version is incremented when
    /// backwards-compatible extensions are made to the API. This allows
    /// the clients to support different features sets.
    /// </summary>
    public required int Minor { get; init; }
}
