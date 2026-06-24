using System;
using System.Collections.Generic;
using Eryph.ModuleCore.Components;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Eryph.ModuleCore.Tests.Components;

public class ComponentBrokerProvisioningTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void CreateRabbitMq_builds_a_provisioner_from_complete_configuration()
    {
        var provisioner = ComponentBrokerProvisioning.CreateRabbitMq(Config(new()
        {
            ["broker:managementUrl"] = "http://rabbit:15672",
            ["broker:managementUser"] = "guest",
            ["broker:managementPassword"] = "guest",
            ["broker:virtualHost"] = "/",
        }));

        provisioner.Should().BeOfType<RabbitMqBrokerProvisioner>();
    }

    [Theory]
    // Each required setting missing in turn must fail fast — the broker management endpoint and an admin
    // credential are the operator's contract, not something to default silently. A malformed (non-
    // absolute / wrong-scheme) management URL fails fast too, with an actionable error rather than a
    // cryptic UriFormatException when the HttpClient base address is built.
    [InlineData(null, "guest", "guest")]
    [InlineData("http://rabbit:15672", null, "guest")]
    [InlineData("http://rabbit:15672", "guest", null)]
    [InlineData("not-a-url", "guest", "guest")]
    [InlineData("ftp://rabbit:15672", "guest", "guest")]
    public void CreateRabbitMq_throws_when_required_configuration_is_missing(
        string? url, string? user, string? password)
    {
        var act = () => ComponentBrokerProvisioning.CreateRabbitMq(Config(new()
        {
            ["broker:managementUrl"] = url,
            ["broker:managementUser"] = user,
            ["broker:managementPassword"] = password,
        }));

        act.Should().Throw<InvalidOperationException>();
    }
}
