using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.Controller.Components;

internal interface IEnvironmentsConfigChangeValidator
{
    /// <summary>
    /// The reasons the proposed environment configuration cannot be applied (empty when it can).
    /// </summary>
    Task<IReadOnlyList<string>> ValidateChanges(string canonicalPayload, CancellationToken cancellationToken);
}

/// <summary>
/// Refuses a change to the environment catalog which would strand existing resources.
/// </summary>
/// <remarks>
/// The site of a resource is pinned when it is created, so re-binding an environment to another site
/// does not move the resources already in it. They would keep their old site while new resources went
/// to the new one, and the environment would no longer be a single locality — the property everything
/// in it can rely on. Removing an environment which is still in use is refused for the same reason.
/// This mirrors the in-use refusals in <c>NetworkConfigValidator.ValidateChanges</c>.
/// </remarks>
internal sealed class EnvironmentsConfigChangeValidator(
    IAuthoredConfigStore authoredConfigStore,
    IStateStore stateStore)
    : IEnvironmentsConfigChangeValidator
{
    public async Task<IReadOnlyList<string>> ValidateChanges(
        string canonicalPayload,
        CancellationToken cancellationToken)
    {
        var current = await authoredConfigStore.GetCurrentAsync(
            ConfigDomain.Environments, ConfigScope.Default, cancellationToken);
        if (current is null)
            return [];

        var currentConfig = EnvironmentsConfigYamlSerializer.Deserialize(current.Payload);
        var newConfig = EnvironmentsConfigYamlSerializer.Deserialize(canonicalPayload);

        var errors = new List<string>();

        foreach (var site in currentConfig.Sites ?? [])
        {
            if (site?.Name is null)
                continue;

            if ((newConfig.Sites ?? []).Any(
                    s => string.Equals(s?.Name, site.Name, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (await SiteHasResources(site.Name, cancellationToken))
                errors.Add(
                    $"The site '{site.Name}' cannot be removed because it still has resources.");
        }

        foreach (var environment in currentConfig.Environments ?? [])
        {
            if (environment?.Name is null)
                continue;

            var updated = (newConfig.Environments ?? [])
                .FirstOrDefault(e => string.Equals(e?.Name, environment.Name, StringComparison.OrdinalIgnoreCase));

            if (updated is not null
                && string.Equals(updated.Site, environment.Site, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!await IsInUse(environment.Name, cancellationToken))
                continue;

            errors.Add(updated is null
                ? $"The environment '{environment.Name}' cannot be removed because it still has resources."
                : $"The environment '{environment.Name}' cannot be moved from the site '{environment.Site}' "
                  + $"to the site '{updated.Site}' because it still has resources. The site of an existing "
                  + "resource cannot change.");
        }

        return errors;
    }

    private async Task<bool> IsInUse(string environment, CancellationToken cancellationToken) =>
        // The site bound resources which carry an environment. A catlet farm is always in the default
        // environment, which is reserved and therefore never part of an authored configuration.
        await stateStore.For<Catlet>().AnyAsync(
            new ResourceSpecs<Catlet>.GetByEnvironmentUnscoped(environment), cancellationToken)
        || await stateStore.For<VirtualDisk>().AnyAsync(
            new ResourceSpecs<VirtualDisk>.GetByEnvironmentUnscoped(environment), cancellationToken)
        || await stateStore.For<VirtualNetwork>().AnyAsync(
            new ResourceSpecs<VirtualNetwork>.GetByEnvironmentUnscoped(environment), cancellationToken);

    private async Task<bool> SiteHasResources(string siteName, CancellationToken cancellationToken)
    {
        var site = await stateStore.For<Site>().GetBySpecAsync(
            new SiteSpecs.GetByName(siteName), cancellationToken);
        if (site is null)
            return false;

        return await stateStore.For<Catlet>().AnyAsync(
                   new SiteBoundSpecs<Catlet>.GetBySiteUnscoped(site.Id), cancellationToken)
               || await stateStore.For<VirtualDisk>().AnyAsync(
                   new SiteBoundSpecs<VirtualDisk>.GetBySiteUnscoped(site.Id), cancellationToken)
               || await stateStore.For<VirtualNetwork>().AnyAsync(
                   new SiteBoundSpecs<VirtualNetwork>.GetBySiteUnscoped(site.Id), cancellationToken)
               || await stateStore.For<CatletFarm>().AnyAsync(
                   new SiteBoundSpecs<CatletFarm>.GetBySiteUnscoped(site.Id), cancellationToken);
    }
}
