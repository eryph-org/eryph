using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb.Model;
using Microsoft.Extensions.Configuration;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the Endpoints domain aggregation: the canonical value per logical endpoint is the
/// operator override (controller "endpoints" config) when set, otherwise the endpoint a component
/// advertised on registration.
/// </summary>
public class EndpointsConfigSourceTests
{
    /// <summary>
    /// Hand-written stub: the registry interface is internal, which Moq cannot proxy without
    /// marking the production assembly InternalsVisibleTo the proxy generator. Only GetActiveAsync
    /// is exercised by EndpointsConfigSource.
    /// </summary>
    private sealed class StubRegistry(IReadOnlyList<ComponentRegistration> active) : IComponentRegistryService
    {
        public Task<ComponentRegistration> UpsertAsync(RegisterComponentCommand command, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task RecordHeartbeatAsync(Guid componentId, Guid instanceId,
            IReadOnlyDictionary<ConfigDomain, long> appliedConfigVersions, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task RecordAppliedAsync(Guid componentId, ConfigDomain domain, long version, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> DeregisterAsync(Guid componentId, Guid instanceId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> RevokeAsync(Guid componentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ComponentRegistration>> GetActiveAsync(CancellationToken cancellationToken)
            => Task.FromResult(active);
    }

    private static EndpointsConfigSource Create(
        IReadOnlyList<ComponentRegistration> activeComponents,
        Dictionary<string, string>? overrides = null)
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.RegisterInstance<IComponentRegistryService>(new StubRegistry(activeComponents));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(BuildOverrideKeys(overrides))
            .Build();

        return new EndpointsConfigSource(configuration, container);
    }

    private static Dictionary<string, string> BuildOverrideKeys(Dictionary<string, string>? overrides)
    {
        var result = new Dictionary<string, string>();
        if (overrides is null) return result;
        foreach (var (key, value) in overrides)
            result[$"endpoints:{key}"] = value;
        return result;
    }

    private static ComponentRegistration Advertising(params (string Name, string Url)[] endpoints)
    {
        var advertised = new Dictionary<string, string>();
        foreach (var (name, url) in endpoints)
            advertised[name] = url;
        return new ComponentRegistration
        {
            MachineName = "host",
            InboundQueue = "q",
            AdvertisedEndpoints = advertised,
        };
    }

    private static async Task<Dictionary<string, string>> Build(EndpointsConfigSource source)
    {
        var payload = await source.BuildPayloadAsync(CancellationToken.None);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(payload)!;
    }

    [Fact]
    public async Task Empty_when_no_components_and_no_override()
    {
        var source = Create([]);
        var result = await Build(source);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Uses_advertised_endpoint_when_not_overridden()
    {
        var source = Create([Advertising(("identity", "https://host:8080/identity"))]);

        var result = await Build(source);

        result.Should().ContainKey("identity").WhoseValue.Should().Be("https://host:8080/identity");
    }

    [Fact]
    public async Task Override_wins_over_advertised()
    {
        var source = Create(
            [Advertising(("identity", "https://host:8080/identity"))],
            overrides: new() { ["identity"] = "https://public-lb/identity" });

        var result = await Build(source);

        // The operator override is authoritative (e.g. the canonical issuer URL behind a load balancer).
        result["identity"].Should().Be("https://public-lb/identity");
    }

    [Fact]
    public async Task Aggregates_advertised_from_multiple_components_with_overrides_layered_on_top()
    {
        var source = Create(
            [
                Advertising(("identity", "https://host:8080/identity")),
                Advertising(("compute", "https://host:8081/compute")),
            ],
            overrides: new() { ["base"] = "https://public/", ["compute"] = "https://public/compute" });

        var result = await Build(source);

        result["identity"].Should().Be("https://host:8080/identity"); // advertised, no override
        result["compute"].Should().Be("https://public/compute");      // override wins
        result["base"].Should().Be("https://public/");                // override only
    }

    [Fact]
    public async Task Derives_default_from_base_when_no_explicit_default()
    {
        var source = Create([], overrides: new() { ["base"] = "https://host:8443/" });

        var result = await Build(source);

        // The resolvers fall back to "default" for unknown/relative endpoints; with only
        // "base" configured it must be derived so consumers always have a base.
        result["default"].Should().Be("https://host:8443/");
        result["base"].Should().Be("https://host:8443/");
    }

    [Fact]
    public async Task Explicit_default_is_not_overridden_by_base()
    {
        var source = Create([], overrides: new()
        {
            ["base"] = "https://host:8443/",
            ["default"] = "https://explicit-default/",
        });

        var result = await Build(source);

        result["default"].Should().Be("https://explicit-default/");
    }
}
