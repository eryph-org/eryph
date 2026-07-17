using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.Controller.Components;

internal interface IEnvironmentsConfigRealizer
{
    /// <summary>
    /// Creates and removes <see cref="Site"/> and <see cref="Eryph.StateDb.Model.Environment"/>
    /// records so they match the authored configuration.
    /// </summary>
    Task RealizeEnvironments(EnvironmentsConfig config, CancellationToken cancellationToken);
}

/// <summary>
/// Materializes the authored environment catalog as records: the sites, and the environment to site
/// bindings they realize. Mirrors how the network provider configuration is realized into provider
/// subnets and IP pools.
/// </summary>
/// <remarks>
/// This is the seam between the two forms of the catalog. The authored form is YAML — versioned,
/// distributed to the components as a configuration domain. The realized form is these tables, and it
/// is the only thing which answers where an environment is. Resolving a site therefore never reads
/// the configuration: the database is seeded long before any configuration is distributed, so a
/// resolution which depended on the exchange could not be used while seeding, which is exactly when
/// networks are created.
/// </remarks>
internal sealed class EnvironmentsConfigRealizer(
    IStateStore stateStore)
    : IEnvironmentsConfigRealizer
{
    public async Task RealizeEnvironments(EnvironmentsConfig config, CancellationToken cancellationToken)
    {
        var authoredSites = (config.Sites ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s?.Name))
            .Select(s => s.Name)
            .ToList();

        var savedSites = await stateStore.For<Site>().ListAsync(cancellationToken);
        var sites = await AddSites(savedSites, authoredSites, cancellationToken);
        await RealizeBindings(config, sites, cancellationToken);
        // Last: a site is only removable once nothing binds to it any more, and the bindings this
        // payload drops are removed above.
        await RemoveSites(savedSites, authoredSites, cancellationToken);
    }

    /// <summary>The sites which exist once this payload is applied, including the ones just added.</summary>
    private async Task<IReadOnlyList<Site>> AddSites(
        IReadOnlyList<Site> savedSites,
        IReadOnlyList<string> authoredSites,
        CancellationToken cancellationToken)
    {
        var sites = savedSites.ToList();

        foreach (var name in authoredSites)
        {
            if (savedSites.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var site = new Site
            {
                Id = Guid.NewGuid(),
                Name = name,
            };
            await stateStore.For<Site>().AddAsync(site, cancellationToken);
            // Kept in the list rather than re-queried: it is only tracked until the unit of work
            // commits, so a query would not see the site this very payload declares.
            sites.Add(site);
        }

        return sites;
    }

    private async Task RealizeBindings(
        EnvironmentsConfig config, IReadOnlyList<Site> sites, CancellationToken cancellationToken)
    {
        var savedEnvironments = await stateStore.For<Eryph.StateDb.Model.Environment>()
            .ListAsync(cancellationToken);

        var authored = (config.Environments ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e?.Name))
            .ToList();

        foreach (var environment in authored)
        {
            var siteName = string.IsNullOrWhiteSpace(environment.Site)
                ? EryphConstants.DefaultSiteName
                : environment.Site;
            var site = sites.FirstOrDefault(
                s => string.Equals(s.Name, siteName, StringComparison.OrdinalIgnoreCase));
            if (site is null)
                throw new InvalidOperationException(
                    $"The environment '{environment.Name}' is configured for the site '{siteName}', "
                    + "which does not exist.");

            var saved = savedEnvironments.FirstOrDefault(
                e => string.Equals(e.Name, environment.Name, StringComparison.OrdinalIgnoreCase));
            if (saved is null)
            {
                await stateStore.For<Eryph.StateDb.Model.Environment>().AddAsync(
                    new Eryph.StateDb.Model.Environment
                    {
                        Name = environment.Name.ToLowerInvariant(),
                        SiteId = site.Id,
                    },
                    cancellationToken);
                continue;
            }

            // Re-binding an environment which still has resources is refused when the configuration
            // is authored. Reaching it here would mean the refusal was bypassed, and the resources
            // pinned to the old site would silently be in an environment realized elsewhere.
            if (saved.SiteId != site.Id)
                saved.SiteId = site.Id;
        }

        foreach (var saved in savedEnvironments)
        {
            if (string.Equals(saved.Name, EryphConstants.DefaultEnvironmentName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (authored.Any(e => string.Equals(e.Name, saved.Name, StringComparison.OrdinalIgnoreCase)))
                continue;

            await stateStore.For<Eryph.StateDb.Model.Environment>().DeleteAsync(saved, cancellationToken);
        }
    }

    private async Task RemoveSites(
        IReadOnlyList<Site> savedSites,
        IReadOnlyList<string> authoredSites,
        CancellationToken cancellationToken)
    {
        // The default site is reserved: it is seeded, never authored, and must not be removed just
        // because it does not appear in the configuration.
        var removedSites = savedSites
            .Where(s => !string.Equals(
                s.Name, EryphConstants.DefaultSiteName, StringComparison.OrdinalIgnoreCase))
            .Where(s => !authoredSites.Any(
                n => string.Equals(n, s.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var site in removedSites)
        {
            // A site with resources cannot be removed. The resources are pinned to it, so removing
            // it would leave them without a location. This is refused when the configuration is
            // authored; the check here is the one the database also enforces via the restricted
            // foreign keys, so it must not be reached in practice.
            if (await HasResources(site.Id, cancellationToken))
                throw new InvalidOperationException(
                    $"The site '{site.Name}' cannot be removed because it still has resources.");

            await stateStore.For<Site>().DeleteAsync(site, cancellationToken);
        }
    }

    private async Task<bool> HasResources(Guid siteId, CancellationToken cancellationToken) =>
        await stateStore.For<Catlet>().AnyAsync(
            new SiteBoundSpecs<Catlet>.GetBySiteUnscoped(siteId), cancellationToken)
        || await stateStore.For<VirtualDisk>().AnyAsync(
            new SiteBoundSpecs<VirtualDisk>.GetBySiteUnscoped(siteId), cancellationToken)
        || await stateStore.For<VirtualNetwork>().AnyAsync(
            new SiteBoundSpecs<VirtualNetwork>.GetBySiteUnscoped(siteId), cancellationToken)
        || await stateStore.For<CatletFarm>().AnyAsync(
            new SiteBoundSpecs<CatletFarm>.GetBySiteUnscoped(siteId), cancellationToken);
}
