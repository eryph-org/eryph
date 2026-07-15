using Eryph.ConfigModel;
using Eryph.Core;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Tests;

/// <summary>
/// Resolves every environment to the default site. For tests which create site bound resources
/// without exercising the environment to site binding itself; see SiteResolverTests for that.
/// </summary>
internal sealed class FakeSiteResolver : ISiteResolver
{
    public Task<Either<Error, Guid>> ResolveSite(
        EnvironmentName environment,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(LanguageExt.Prelude.Right<Error, Guid>(EryphConstants.DefaultSiteId));
}
