using System.Collections.Generic;
using Eryph.ModuleCore;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Eryph.ModuleCore.Tests;

public class ComponentPublicEndpointTests
{
    [Fact]
    public void Get_returns_the_configured_public_url()
    {
        var config = Build(("endpoints:public", "https://identity-lb:8080/"));

        ComponentPublicEndpoint.Get(config)!.ToString().Should().Be("https://identity-lb:8080/");
    }

    [Fact]
    public void Get_returns_null_when_unset_or_not_absolute()
    {
        ComponentPublicEndpoint.Get(Build()).Should().BeNull();
        ComponentPublicEndpoint.Get(Build(("endpoints:public", "not a url"))).Should().BeNull();
    }

    [Fact]
    public void GetServerDnsNames_defaults_to_the_public_url_host()
    {
        var config = Build(("endpoints:public", "https://compute-lb:8000/"));

        ComponentPublicEndpoint.GetServerDnsNames(config).Should().Equal("compute-lb");
    }

    [Fact]
    public void GetServerDnsNames_uses_the_explicit_override_when_set()
    {
        // The explicit multi-SAN override wins over the public-url host.
        var config = Build(
            ("endpoints:public", "https://compute-lb:8000/"),
            ("componentMtls:serverDnsNames", "compute-lb, compute.example.test"));

        ComponentPublicEndpoint.GetServerDnsNames(config)
            .Should().Equal("compute-lb", "compute.example.test");
    }

    [Fact]
    public void GetServerDnsNames_is_empty_when_neither_is_configured()
    {
        ComponentPublicEndpoint.GetServerDnsNames(Build()).Should().BeEmpty();
    }

    private static IConfiguration Build(params (string Key, string Value)[] values)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in values)
            dict[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }
}
