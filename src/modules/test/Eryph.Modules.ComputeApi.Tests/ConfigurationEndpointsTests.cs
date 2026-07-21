using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.ComputeApi.Configuration;
using Eryph.Modules.ComputeApi.Endpoints.V1.Configuration;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.ComputeApi.Tests;

public class ConfigurationEndpointsTests
{
    private sealed class FakeEnvironmentsConfigProvider(EnvironmentsConfig current) : IEnvironmentsConfigProvider
    {
        public EnvironmentsConfig Current { get; } = current;
        public void Update(EnvironmentsConfig config) { }
    }

    private sealed class FakeStorageConfigProvider(StorageConfig current) : IStorageConfigProvider
    {
        public StorageConfig Current { get; } = current;
        public void Update(StorageConfig config) { }
    }

    [Fact]
    public async Task ListSites_EmptyCatalog_ReturnsOnlyDefaultSite()
    {
        var endpoint = new ListSites(new FakeEnvironmentsConfigProvider(new EnvironmentsConfig()));

        var result = await endpoint.HandleAsync();

        result.Value!.Value.Should().ContainSingle()
            .Which.Name.Should().Be(EryphConstants.DefaultSiteName);
    }

    [Fact]
    public async Task ListSites_IncludesDistributedSites_AndDeduplicatesDefault()
    {
        var config = new EnvironmentsConfig
        {
            // An authored payload never re-declares the reserved default, but a stray duplicate must
            // still collapse to a single entry.
            Sites = [new SiteConfig { Name = "berlin" }, new SiteConfig { Name = EryphConstants.DefaultSiteName }],
        };
        var endpoint = new ListSites(new FakeEnvironmentsConfigProvider(config));

        var result = await endpoint.HandleAsync();

        result.Value!.Value.Should().SatisfyRespectively(
            site => site.Name.Should().Be(EryphConstants.DefaultSiteName),
            site => site.Name.Should().Be("berlin"));
    }

    [Fact]
    public async Task ListEnvironments_EmptyCatalog_ReturnsDefaultEnvironmentOnDefaultSite()
    {
        var endpoint = new ListEnvironments(new FakeEnvironmentsConfigProvider(new EnvironmentsConfig()));

        var result = await endpoint.HandleAsync();

        var environment = result.Value!.Value.Should().ContainSingle().Which;
        environment.Name.Should().Be(EryphConstants.DefaultEnvironmentName);
        environment.Site.Should().Be(EryphConstants.DefaultSiteName);
    }

    [Fact]
    public async Task ListEnvironments_MapsSite_AndFallsBackToDefaultSiteWhenMissing()
    {
        var config = new EnvironmentsConfig
        {
            Environments =
            [
                new EnvironmentConfig { Name = "prod", Site = "berlin" },
                new EnvironmentConfig { Name = "test", Site = "" },
            ],
        };
        var endpoint = new ListEnvironments(new FakeEnvironmentsConfigProvider(config));

        var result = await endpoint.HandleAsync();

        result.Value!.Value.Should().SatisfyRespectively(
            env =>
            {
                env.Name.Should().Be(EryphConstants.DefaultEnvironmentName);
                env.Site.Should().Be(EryphConstants.DefaultSiteName);
            },
            env =>
            {
                env.Name.Should().Be("prod");
                env.Site.Should().Be("berlin");
            },
            env =>
            {
                env.Name.Should().Be("test");
                env.Site.Should().Be(EryphConstants.DefaultSiteName);
            });
    }

    [Fact]
    public async Task ListDatastores_EmptyCatalog_ReturnsOnlyDefaultDatastore()
    {
        var endpoint = new ListDatastores(new FakeStorageConfigProvider(new StorageConfig()));

        var result = await endpoint.HandleAsync();

        result.Value!.Value.Should().ContainSingle()
            .Which.Name.Should().Be(EryphConstants.DefaultDataStoreName);
    }

    [Fact]
    public async Task ListDatastores_UnionsGlobalAndEnvironmentScoped_AndDeduplicates()
    {
        var config = new StorageConfig
        {
            Datastores = [new StorageDatastoreConfig { Name = "fast" }],
            Environments =
            [
                new StorageEnvironmentConfig
                {
                    Name = "prod",
                    // 'fast' repeats the global entry (must dedup); 'archive' is env-only (must appear).
                    Datastores =
                        [new StorageDatastoreConfig { Name = "fast" }, new StorageDatastoreConfig { Name = "archive" }],
                },
            ],
        };
        var endpoint = new ListDatastores(new FakeStorageConfigProvider(config));

        var result = await endpoint.HandleAsync();

        result.Value!.Value.Should().SatisfyRespectively(
            ds => ds.Name.Should().Be(EryphConstants.DefaultDataStoreName),
            ds => ds.Name.Should().Be("fast"),
            ds => ds.Name.Should().Be("archive"));
    }

    // A YAML payload with an omitted or explicit-null section (e.g. `sites: ~`) deserializes the array
    // to null, which the model comments call out. These tests set the arrays to null explicitly so the
    // `?? []` guards in the endpoints are actually exercised (new EnvironmentsConfig()/StorageConfig()
    // default them to empty, not null, so the other tests do not cover this path).

    [Fact]
    public async Task ListSites_NullSitesArray_ReturnsOnlyDefault()
    {
        var endpoint = new ListSites(new FakeEnvironmentsConfigProvider(new EnvironmentsConfig { Sites = null }));

        var result = await endpoint.HandleAsync();

        result.Value!.Value.Should().ContainSingle()
            .Which.Name.Should().Be(EryphConstants.DefaultSiteName);
    }

    [Fact]
    public async Task ListEnvironments_NullEnvironmentsArray_ReturnsOnlyDefault()
    {
        var endpoint = new ListEnvironments(
            new FakeEnvironmentsConfigProvider(new EnvironmentsConfig { Environments = null }));

        var result = await endpoint.HandleAsync();

        result.Value!.Value.Should().ContainSingle()
            .Which.Name.Should().Be(EryphConstants.DefaultEnvironmentName);
    }

    [Fact]
    public async Task ListDatastores_NullArrays_ReturnsOnlyDefault()
    {
        var endpoint = new ListDatastores(
            new FakeStorageConfigProvider(new StorageConfig { Datastores = null, Environments = null }));

        var result = await endpoint.HandleAsync();

        result.Value!.Value.Should().ContainSingle()
            .Which.Name.Should().Be(EryphConstants.DefaultDataStoreName);
    }

    [Fact]
    public async Task ListSites_SkipsWhitespaceNames_AndDeduplicatesCaseInsensitively()
    {
        var config = new EnvironmentsConfig
        {
            // '   ' must be skipped; 'Berlin' then 'berlin' must collapse to the first-seen 'Berlin'.
            Sites =
            [
                new SiteConfig { Name = "   " },
                new SiteConfig { Name = "Berlin" },
                new SiteConfig { Name = "berlin" },
            ],
        };
        var endpoint = new ListSites(new FakeEnvironmentsConfigProvider(config));

        var result = await endpoint.HandleAsync();

        result.Value!.Value.Should().SatisfyRespectively(
            site => site.Name.Should().Be(EryphConstants.DefaultSiteName),
            site => site.Name.Should().Be("Berlin"));
    }
}
