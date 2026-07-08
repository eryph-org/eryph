using Eryph.Core;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Controller.Components;
using LanguageExt.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// The NetworkProviders source serves the value from the (authored-or-file) network provider manager
/// verbatim. NetworkProviders is a single global topology, so the scope parameter is not used —
/// non-default authoring is rejected upstream (see SetConfigDomainCommandHandler).
/// </summary>
public class NetworkProvidersConfigSourceTests
{
    private static NetworkProvidersConfigSource Create(Mock<INetworkProviderManager> manager) =>
        new(manager.Object, NullLogger<NetworkProvidersConfigSource>.Instance);

    [Fact]
    public async Task BuildPayloadAsync_returns_the_manager_configuration_verbatim()
    {
        var manager = new Mock<INetworkProviderManager>();
        manager.Setup(m => m.GetCurrentConfigurationYaml())
            .Returns(RightAsync<Error, string>("network_providers: []"));

        var payload = await Create(manager).BuildPayloadAsync(ConfigScope.Default, default);

        payload.Should().Be("network_providers: []");
    }

    [Fact]
    public async Task BuildPayloadAsync_throws_when_the_configuration_cannot_be_read()
    {
        var manager = new Mock<INetworkProviderManager>();
        manager.Setup(m => m.GetCurrentConfigurationYaml())
            .Returns(LeftAsync<Error, string>(Error.New("p_networks.yml is unreadable")));

        await Create(manager).Invoking(s => s.BuildPayloadAsync(ConfigScope.Default, default))
            .Should().ThrowAsync<System.Exception>();
    }
}
