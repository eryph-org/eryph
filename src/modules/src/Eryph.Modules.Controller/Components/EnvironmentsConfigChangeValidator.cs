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
        var newConfig = EnvironmentsConfigYamlSerializer.Deserialize(canonicalPayload);
        var errors = new List<string>();

        // Where the resources already are, not what the previous configuration said. Resources can
        // exist in an environment that was never authored — the inventory records a catlet with the
        // environment derived from its path, without consulting the configuration — so comparing the
        // two payloads would miss them entirely on the first authoring, and a differential check
        // cannot see an environment that only the resources know about.
        foreach (var environment in newConfig.Environments ?? [])
        {
            if (environment?.Name is null)
                continue;

            // The site may be declared by this very payload and not exist yet — it is realized only
            // once the payload is accepted. Nothing is pinned to it then, so every resource of the
            // environment is somewhere else and would be stranded just the same.
            var declaredSite = await stateStore.For<Site>().GetBySpecAsync(
                new SiteSpecs.GetByName(environment.Site), cancellationToken);

            var strandedSites = await FindSitesOfResources(
                environment.Name, declaredSite?.Id, cancellationToken);
            if (strandedSites.Count == 0)
                continue;

            errors.Add(
                $"The environment '{environment.Name}' cannot be configured for the site "
                + $"'{environment.Site}' because it already has resources in another site. The site "
                + "of an existing resource cannot change.");
        }

        var current = await authoredConfigStore.GetCurrentAsync(
            ConfigDomain.Environments, ConfigScope.Default, cancellationToken);
        if (current is null)
            return errors;

        var currentConfig = EnvironmentsConfigYamlSerializer.Deserialize(current.Payload);

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

            if ((newConfig.Environments ?? []).Any(
                    e => string.Equals(e?.Name, environment.Name, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (await IsInUse(environment.Name, cancellationToken))
                errors.Add(
                    $"The environment '{environment.Name}' cannot be removed because it still has resources.");
        }

        return errors;
    }

    /// <summary>
    /// The sites of the resources in an environment, other than the one given. Empty when every
    /// resource in it is already where the configuration says the environment is. A null
    /// <paramref name="declaredSiteId"/> means the site does not exist yet, so no resource can be in
    /// it and every one of them counts.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> FindSitesOfResources(
        string environment, Guid? declaredSiteId, CancellationToken cancellationToken)
    {
        var catlets = await stateStore.For<Catlet>().ListAsync(
            new ResourceSpecs<Catlet>.GetByEnvironmentUnscoped(environment), cancellationToken);
        var disks = await stateStore.For<VirtualDisk>().ListAsync(
            new ResourceSpecs<VirtualDisk>.GetByEnvironmentUnscoped(environment), cancellationToken);
        var networks = await stateStore.For<VirtualNetwork>().ListAsync(
            new ResourceSpecs<VirtualNetwork>.GetByEnvironmentUnscoped(environment), cancellationToken);

        return catlets.Select(c => c.SiteId)
            .Concat(disks.Select(d => d.SiteId))
            .Concat(networks.Select(n => n.SiteId))
            .Where(id => !declaredSiteId.HasValue || id != declaredSiteId.Value)
            .Distinct()
            .ToList();
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
