using Eryph.Core;
using Eryph.Core.Network;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Controller.Components;
using LanguageExt.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// The NetworkProviders source serves the controller's network-provider configuration, stripping the
/// runtime IP-pool cursor so an allocation does not churn the distributed payload. NetworkProviders is a
/// single global topology, so the scope parameter is not used.
/// </summary>
public class NetworkProvidersConfigSourceTests
{
    private const string ConfigWithCursor =
        "network_providers:\n"
        + "- name: default\n"
        + "  type: nat_overlay\n"
        + "  bridge_name: br-nat\n"
        + "  subnets:\n"
        + "  - name: default\n"
        + "    network: 10.249.248.0/22\n"
        + "    gateway: 10.249.248.1\n"
        + "    ip_pools:\n"
        + "    - name: default\n"
        + "      first_ip: 10.249.248.10\n"
        + "      next_ip: 10.249.248.50\n"
        + "      last_ip: 10.249.251.254\n";

    private static NetworkProvidersConfigSource Create(Mock<INetworkProviderManager> manager) =>
        new(manager.Object, NullLogger<NetworkProvidersConfigSource>.Instance);

    [Fact]
    public async Task BuildPayloadAsync_strips_the_ip_pool_next_ip_cursor()
    {
        var config = NetworkProvidersConfigYamlSerializer.Deserialize(ConfigWithCursor);
        var manager = new Mock<INetworkProviderManager>();
        manager.Setup(m => m.GetCurrentConfiguration())
            .Returns(RightAsync<Error, NetworkProvidersConfiguration>(config));

        var payload = await Create(manager).BuildPayloadAsync(ConfigScope.Default, default);

        // The next-IP cursor is runtime state, not distributed config: stripping it keeps a pure IP
        // allocation from changing the payload and re-pushing the whole fleet.
        payload.Should().NotContain("next_ip");
        payload.Should().Contain("first_ip: 10.249.248.10");
    }

    [Fact]
    public async Task BuildPayloadAsync_throws_when_the_configuration_cannot_be_read()
    {
        var manager = new Mock<INetworkProviderManager>();
        manager.Setup(m => m.GetCurrentConfiguration())
            .Returns(LeftAsync<Error, NetworkProvidersConfiguration>(Error.New("p_networks.yml is unreadable")));

        await Create(manager).Invoking(s => s.BuildPayloadAsync(ConfigScope.Default, default))
            .Should().ThrowAsync<System.Exception>();
    }
}
