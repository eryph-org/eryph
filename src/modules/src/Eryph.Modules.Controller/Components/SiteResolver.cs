using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Components;

internal sealed class SiteResolver(
    IAuthoredConfigStore authoredConfigStore,
    IStateStoreRepository<Site> siteRepository)
    : ISiteResolver
{
    public async Task<Either<Error, Guid>> ResolveSite(
        EnvironmentName environment,
        CancellationToken cancellationToken = default)
    {
        var siteName = await FindSiteName(environment, cancellationToken);
        if (siteName is null)
            return Error.New(
                $"The environment '{environment}' is not part of the environment configuration.");

        var site = await siteRepository.GetBySpecAsync(
            new SiteSpecs.GetByName(siteName), cancellationToken);

        return site is null
            ? Error.New(
                $"The environment '{environment}' is configured for the site '{siteName}', "
                + "which does not exist.")
            : Right<Error, Guid>(site.Id);
    }

    private async Task<string?> FindSiteName(
        EnvironmentName environment,
        CancellationToken cancellationToken)
    {
        // The default environment is reserved and therefore never authored; it always resolves to
        // the default site without consulting the configuration.
        if (string.Equals(
                environment.Value, EryphConstants.DefaultEnvironmentName, StringComparison.OrdinalIgnoreCase))
            return EryphConstants.DefaultSiteName;

        var authored = await authoredConfigStore.GetCurrentAsync(
            ConfigDomain.Environments, ConfigScope.Default, cancellationToken);
        if (authored is null)
            return null;

        var config = EnvironmentsConfigYamlSerializer.Deserialize(authored.Payload);
        return EnvironmentsConfigValidation.FindSite(config, environment.Value);
    }
}
