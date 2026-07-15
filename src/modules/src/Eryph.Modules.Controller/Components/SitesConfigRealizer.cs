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

internal interface ISitesConfigRealizer
{
    /// <summary>
    /// Creates and removes <see cref="Site"/> records so they match the authored configuration.
    /// </summary>
    Task RealizeSites(EnvironmentsConfig config, CancellationToken cancellationToken);
}

/// <summary>
/// Materializes the authored sites as records, so the sites an environment can reference actually
/// exist and resources can be pinned to them. Mirrors how the network provider configuration is
/// realized into provider subnets and IP pools.
/// </summary>
internal sealed class SitesConfigRealizer(
    IStateStore stateStore)
    : ISitesConfigRealizer
{
    public async Task RealizeSites(EnvironmentsConfig config, CancellationToken cancellationToken)
    {
        var authoredSites = (config.Sites ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s?.Name))
            .Select(s => s.Name)
            .ToList();

        var savedSites = await stateStore.For<Site>().ListAsync(cancellationToken);

        foreach (var name in authoredSites)
        {
            if (savedSites.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;

            await stateStore.For<Site>().AddAsync(
                new Site
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                },
                cancellationToken);
        }

        await RemoveSites(savedSites, authoredSites, cancellationToken);
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
