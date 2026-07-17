using Eryph.ConfigModel;
using Eryph.Core;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Tests;

/// <summary>
/// Resolves the environments it was given and the default environment to the default site.
/// For tests which need environments to sit in different sites; see SiteResolverTests for the
/// resolution against the authored catalog itself.
/// </summary>
internal sealed class SiteAwareSiteResolver(params (string Environment, Guid SiteId)[] sites)
    : ISiteResolver
{
    public Task<Either<Error, Guid>> ResolveSite(
        EnvironmentName environment,
        CancellationToken cancellationToken = default)
    {
        foreach (var (name, siteId) in sites)
        {
            if (string.Equals(name, environment.Value, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(LanguageExt.Prelude.Right<Error, Guid>(siteId));
        }

        return Task.FromResult(LanguageExt.Prelude.Right<Error, Guid>(EryphConstants.DefaultSiteId));
    }
}
