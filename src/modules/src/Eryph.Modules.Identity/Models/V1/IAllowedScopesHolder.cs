using System.Collections.Generic;

namespace Eryph.Modules.Identity.Models.V1;

public interface IAllowedScopesHolder
{
    /// <summary>
    /// The allowed scopes for the client.
    /// </summary>
    IReadOnlyList<string> AllowedScopes { get; set; }
}
