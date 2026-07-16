using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Components;

/// <summary>
/// Resolves the site of an environment from the realized catalog, never from the authored
/// configuration. See <see cref="EnvironmentsConfigRealizer"/> for why the two are separate.
/// </summary>
internal sealed class SiteResolver(
    IStateStoreRepository<Eryph.StateDb.Model.Environment> environmentRepository)
    : ISiteResolver
{
    public async Task<Either<Error, Guid>> ResolveSite(
        EnvironmentName environment,
        CancellationToken cancellationToken = default)
    {
        // The default environment is reserved: it always exists and is always realized by the
        // default site, so it is neither authored nor realized as a record.
        if (string.Equals(
                environment.Value, EryphConstants.DefaultEnvironmentName, StringComparison.OrdinalIgnoreCase))
            return EryphConstants.DefaultSiteId;

        var realized = await environmentRepository.GetBySpecAsync(
            new EnvironmentSpecs.GetByName(environment.Value), cancellationToken);

        return realized is null
            ? Error.New($"The environment '{environment}' is not part of the environment configuration.")
            : Right<Error, Guid>(realized.SiteId);
    }
}
