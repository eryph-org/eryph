using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb.Model;
using LanguageExt.Common;
using Moq;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the decorator that overlays the operator-authored NetworkProviders value on the read path:
/// authored wins when present, the local file is the fallback, and writes always target the file (never
/// the authored store) so the IP-cursor write-back does not append authored versions.
/// </summary>
public class AuthoredNetworkProviderManagerTests
{
    private static AuthoredNetworkProviderManager Create(
        Mock<INetworkProviderManager> inner, AuthoredConfig? authored)
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.RegisterInstance<IAuthoredConfigStore>(new StubAuthoredStore(authored));
        return new AuthoredNetworkProviderManager(inner.Object, container);
    }

    private static AuthoredConfig Authored(string payload) => new()
    {
        Id = Guid.NewGuid(), Domain = ConfigDomain.NetworkProviders,
        Scope = "", Version = 1, Payload = payload,
    };

    [Fact]
    public async Task GetCurrentConfigurationYaml_returns_the_authored_value_without_reading_the_file()
    {
        var inner = new Mock<INetworkProviderManager>();
        var manager = Create(inner, Authored("authored-yaml"));

        var yaml = await manager.GetCurrentConfigurationYaml().IfLeft(e => throw new Exception(e.Message));

        yaml.Should().Be("authored-yaml");
        inner.Verify(m => m.GetCurrentConfigurationYaml(), Times.Never);
    }

    [Fact]
    public async Task GetCurrentConfigurationYaml_falls_back_to_the_file_when_not_authored()
    {
        var inner = new Mock<INetworkProviderManager>();
        inner.Setup(m => m.GetCurrentConfigurationYaml())
            .Returns(RightAsync<Error, string>("file-yaml"));
        var manager = Create(inner, authored: null);

        var yaml = await manager.GetCurrentConfigurationYaml().IfLeft(e => throw new Exception(e.Message));

        yaml.Should().Be("file-yaml");
    }

    [Fact]
    public async Task Save_always_targets_the_file_never_the_authored_store()
    {
        var inner = new Mock<INetworkProviderManager>();
        inner.Setup(m => m.SaveConfigurationYaml(It.IsAny<string>()))
            .Returns(RightAsync<Error, LanguageExt.Unit>(unit));
        // Even with an authored value present, the write goes to the file.
        var manager = Create(inner, Authored("authored-yaml"));

        await manager.SaveConfigurationYaml("new-file-yaml").IfLeft(e => throw new Exception(e.Message));

        inner.Verify(m => m.SaveConfigurationYaml("new-file-yaml"), Times.Once);
    }

    private sealed class StubAuthoredStore(AuthoredConfig? current) : IAuthoredConfigStore
    {
        public Task<AuthoredConfig?> GetCurrentAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => Task.FromResult(current);

        public Task<AuthoredConfig> AddVersionAsync(
            ConfigDomain domain, string scope, string payload, string? author, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<AuthoredConfig>> GetHistoryAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
