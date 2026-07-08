using Eryph.Core;
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
/// The storage source is authoritative from the operator-authored store once a value exists, and falls
/// back to the host-wired <see cref="IStorageConfigDefaultsProvider"/> until the domain is first
/// authored. The scoped store is resolved from the container in a dedicated scope, so the source is
/// constructed with the container (mirrors EndpointsConfigSource).
/// </summary>
public class StorageConfigSourceTests
{
    private static StorageConfigSource Create(AuthoredConfig? authored, IStorageConfigDefaultsProvider defaults)
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.RegisterInstance<IAuthoredConfigStore>(new StubStore(authored));
        return new StorageConfigSource(container, defaults, NullLogger<StorageConfigSource>.Instance);
    }

    [Fact]
    public async Task Uses_the_authored_value_when_present_without_reading_the_defaults()
    {
        var defaults = new Mock<IStorageConfigDefaultsProvider>();
        var source = Create(
            new AuthoredConfig
            {
                Id = Guid.NewGuid(), Domain = ConfigDomain.StorageConfig,
                Scope = ConfigScope.Default, Version = 3, Payload = "authored-yaml",
            },
            defaults.Object);

        var payload = await source.BuildPayloadAsync(ConfigScope.Default, default);

        payload.Should().Be("authored-yaml");
        defaults.Verify(m => m.GetDefaultStorageConfig(), Times.Never);
    }

    [Fact]
    public async Task Falls_back_to_the_host_defaults_when_not_yet_authored()
    {
        var defaults = new Mock<IStorageConfigDefaultsProvider>();
        defaults.Setup(m => m.GetDefaultStorageConfig())
            .Returns(RightAsync<Error, StorageConfig>(new StorageConfig
            {
                Datastores = [new StorageDatastoreConfig { Name = "ds1", Path = @"D:\ds1" }],
                Environments = [new StorageEnvironmentConfig { Name = "env1" }],
            }));

        var source = Create(null, defaults.Object);

        var payload = await source.BuildPayloadAsync(ConfigScope.Default, default);

        var storage = StorageConfigYamlSerializer.Deserialize(payload);
        storage.Datastores.Should().ContainSingle().Which.Path.Should().Be(@"D:\ds1");
        storage.Environments.Select(e => e.Name).Should().BeEquivalentTo("env1");
    }

    [Fact]
    public async Task Uses_the_authored_value_at_a_non_default_scope()
    {
        var defaults = new Mock<IStorageConfigDefaultsProvider>();
        var source = Create(
            new AuthoredConfig
            {
                Id = Guid.NewGuid(), Domain = ConfigDomain.StorageConfig,
                Scope = "env:edge", Version = 1, Payload = "edge-yaml",
            },
            defaults.Object);

        var payload = await source.BuildPayloadAsync("env:edge", default);

        payload.Should().Be("edge-yaml");
        defaults.Verify(m => m.GetDefaultStorageConfig(), Times.Never);
    }

    [Fact]
    public async Task Throws_for_an_unauthored_non_default_scope_instead_of_the_defaults_fallback()
    {
        // The defaults fallback is only valid for the default scope; a non-default scope with no
        // authored value should never be materialized (resolution only picks authored scopes), so this
        // is an invariant guard, not the fallback.
        var defaults = new Mock<IStorageConfigDefaultsProvider>();
        var source = Create(null, defaults.Object);

        var act = () => source.BuildPayloadAsync("env:edge", default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        defaults.Verify(m => m.GetDefaultStorageConfig(), Times.Never);
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
