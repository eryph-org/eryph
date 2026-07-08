using Eryph.Core;
using Eryph.Core.Settings;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Configuration;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb.Model;
using LanguageExt.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// The placement source is authoritative from the operator-authored store once a value exists, and
/// falls back to the controller settings file until the domain is first authored via the API. The
/// scoped store is resolved from the container in a dedicated scope, so the source is constructed with
/// the container (mirrors EndpointsConfigSource).
/// </summary>
public class StorageConfigSourceTests
{
    private static StorageConfigSource Create(AuthoredConfig? authored, IControllerSettingsManager settings)
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.RegisterInstance<IAuthoredConfigStore>(new StubStore(authored));
        return new StorageConfigSource(container, settings, NullLogger<StorageConfigSource>.Instance);
    }

    [Fact]
    public async Task Uses_the_authored_value_when_present_without_reading_the_file()
    {
        var settings = new Mock<IControllerSettingsManager>();
        var source = Create(
            new AuthoredConfig
            {
                Id = Guid.NewGuid(), Domain = ConfigDomain.StorageConfig,
                Scope = ConfigScope.Default, Version = 3, Payload = "authored-yaml",
            },
            settings.Object);

        var payload = await source.BuildPayloadAsync(ConfigScope.Default, default);

        payload.Should().Be("authored-yaml");
        settings.Verify(m => m.GetCurrentConfiguration(), Times.Never);
    }

    [Fact]
    public async Task Falls_back_to_the_settings_file_when_not_yet_authored()
    {
        var settings = new Mock<IControllerSettingsManager>();
        settings.Setup(m => m.GetCurrentConfiguration())
            .Returns(RightAsync<Error, ControllerSettings>(new ControllerSettings
            {
                Storage = new StorageConfig { Datastores = ["ds1"], Environments = ["env1"] },
            }));

        var source = Create(null, settings.Object);

        var payload = await source.BuildPayloadAsync(ConfigScope.Default, default);

        var placement = StorageConfigYamlSerializer.Deserialize(payload);
        placement.Datastores.Should().BeEquivalentTo("ds1");
        placement.Environments.Should().BeEquivalentTo("env1");
    }

    [Fact]
    public async Task Uses_the_authored_value_at_a_non_default_scope()
    {
        var settings = new Mock<IControllerSettingsManager>();
        var source = Create(
            new AuthoredConfig
            {
                Id = Guid.NewGuid(), Domain = ConfigDomain.StorageConfig,
                Scope = "env:edge", Version = 1, Payload = "edge-yaml",
            },
            settings.Object);

        var payload = await source.BuildPayloadAsync("env:edge", default);

        payload.Should().Be("edge-yaml");
        settings.Verify(m => m.GetCurrentConfiguration(), Times.Never);
    }

    [Fact]
    public async Task Throws_for_an_unauthored_non_default_scope_instead_of_the_file_fallback()
    {
        // The settings-file fallback is only valid for the default scope; a non-default scope with no
        // authored value should never be materialized (resolution only picks authored scopes), so this
        // is an invariant guard, not the file fallback.
        var settings = new Mock<IControllerSettingsManager>();
        var source = Create(null, settings.Object);

        var act = () => source.BuildPayloadAsync("env:edge", default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        settings.Verify(m => m.GetCurrentConfiguration(), Times.Never);
    }

    private sealed class StubStore(AuthoredConfig? current) : IAuthoredConfigStore
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
