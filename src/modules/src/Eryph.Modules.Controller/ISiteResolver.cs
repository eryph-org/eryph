using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller;

/// <summary>
/// Resolves the site which realizes an environment, from the operator-authored environment catalog.
/// </summary>
/// <remarks>
/// This is only consulted when a resource is created, to decide the site it is pinned to. It must
/// never be used to look up the site of an existing resource: that site is pinned on the resource
/// itself, and re-deriving it would silently relocate resources whenever the catalog is re-authored.
/// </remarks>
public interface ISiteResolver
{
    /// <summary>
    /// The id of the site realizing the environment, or an error when the environment is unknown.
    /// </summary>
    Task<Either<Error, Guid>> ResolveSite(
        EnvironmentName environment,
        CancellationToken cancellationToken = default);
}
