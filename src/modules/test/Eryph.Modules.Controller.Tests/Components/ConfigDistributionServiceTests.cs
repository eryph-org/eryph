using Eryph.Core;
using Eryph.Core.Settings;
using Eryph.DistributedLock;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the config-distribution model: which entitled domains a component is sent, how records are
/// materialized and versioned, and drift detection. Nothing is authored here, so every domain resolves
/// the default scope; scope resolution itself is covered by <see cref="ConfigScopeTests"/>.
/// </summary>
public class ConfigDistributionServiceTests
{
    private static readonly EmptyAuthoredStore Authored = new();

    // A default-scope registration (no environment/tags), so config resolves the default scope.
    private static ComponentRegistration Reg(
        ComponentType type, IEnumerable<AppliedConfigVersion>? applied = null)
    {
        var registration = new ComponentRegistration
        {
            ComponentId = Guid.NewGuid(),
            ComponentType = type,
            MachineName = "host",
            InboundQueue = "queue",
        };
        if (applied is not null)
            registration.SetAppliedVersions(applied);
        return registration;
    }

    // Builds a single-entry applied-versions list at the default scope, the shape most tests need.
    private static AppliedConfigVersion Applied(ConfigDomain domain, long version, string scope = "") =>
        new() { Domain = domain, Scope = scope, Version = version };

    private static ConfigDistributionService CreateService(
        ControllerSettings settings,
        Mock<IStateStoreRepository<ConfigRecord>> records)
    {
        var defaults = new Mock<IStorageConfigDefaultsProvider>();
        defaults.Setup(m => m.GetDefaultStorageConfig())
            .Returns(RightAsync<Error, StorageConfig>(settings.Storage));

        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        // Nothing authored via the API, so StorageConfigSource falls back to the host defaults provider.
        // The scoped store is resolved from the container; IAuthoredConfigStore is internal so it is
        // hand-stubbed.
        container.RegisterInstance<IAuthoredConfigStore>(Authored);
        var source = new StorageConfigSource(
            container, defaults.Object, NullLogger<StorageConfigSource>.Instance);
        return new ConfigDistributionService(
            records.Object, new IConfigSource[] { source }, Authored, NoOpLock());
    }

    private static ConfigDistributionService CreateService(
        Mock<IStateStoreRepository<ConfigRecord>> records,
        params IConfigSource[] sources) =>
        new(records.Object, sources, Authored, NoOpLock());

    private sealed class EmptyAuthoredStore : IAuthoredConfigStore
    {
        public Task<AuthoredConfig?> GetCurrentAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => Task.FromResult<AuthoredConfig?>(null);

        public Task<AuthoredConfig> AddVersionAsync(
            ConfigDomain domain, string scope, string payload, string? author, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<AuthoredConfig>> GetHistoryAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    // The distributed lock only serializes concurrent access; for single-threaded unit tests a
    // no-op holder (AcquireLock returns a completed ValueTask by default) is sufficient.
    private static IDistributedLockScopeHolder NoOpLock() =>
        new Mock<IDistributedLockScopeHolder>().Object;

    [Fact]
    public async Task BuildSnapshot_for_entitled_component_returns_storage_bundle_from_settings()
    {
        var settings = new ControllerSettings
        {
            Storage = new StorageConfig
            {
                Datastores = [new StorageDatastoreConfig { Name = "ds1", Path = @"D:\ds1" }],
                Environments = [new StorageEnvironmentConfig { Name = "env1" }],
            },
        };

        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord?)null);
        records.Setup(r => r.AddAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord record, CancellationToken _) => record);

        var service = CreateService(settings, records);

        var bundles = await service.BuildSnapshotAsync(
            Reg(ComponentType.VMHostAgent), new List<AppliedConfigVersion>(), CancellationToken.None);

        bundles.Should().ContainSingle();
        bundles[0].Domain.Should().Be(ConfigDomain.StorageConfig);
        bundles[0].Version.Should().Be(1);

        var payload = StorageConfigYamlSerializer.Deserialize(bundles[0].Payload);
        payload.Datastores.Should().ContainSingle().Which.Path.Should().Be(@"D:\ds1");
        payload.Environments.Should().ContainSingle().Which.Name.Should().Be("env1");
    }

    [Fact]
    public async Task BuildSnapshot_returns_every_entitled_domain_that_has_a_source()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord?)null);
        records.Setup(r => r.AddAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord record, CancellationToken _) => record);

        // The host agent is entitled to both storage and network-provider config.
        var service = CreateService(records,
            new StubSource(ConfigDomain.StorageConfig, """{"p":1}"""),
            new StubSource(ConfigDomain.NetworkProviders, "network_providers: []"));

        var bundles = await service.BuildSnapshotAsync(
            Reg(ComponentType.VMHostAgent), new List<AppliedConfigVersion>(), CancellationToken.None);

        bundles.Select(b => b.Domain).Should().BeEquivalentTo(
            [ConfigDomain.StorageConfig, ConfigDomain.NetworkProviders]);
    }

    [Fact]
    public async Task BuildSnapshot_for_unentitled_component_is_empty()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        var service = CreateService(new ControllerSettings(), records);

        var bundles = await service.BuildSnapshotAsync(
            Reg(ComponentType.ComputeApi), new List<AppliedConfigVersion>(), CancellationToken.None);

        bundles.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildSnapshot_omits_domain_the_component_already_has_at_current_version()
    {
        // The stored record matches the source payload, so re-evaluation does not
        // bump the version; the component already holds that version, so nothing is sent.
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigRecord
            {
                Id = Guid.NewGuid(),
                Domain = ConfigDomain.StorageConfig,
                Scope = "",
                Version = 3,
                Payload = """{"v":1}""",
            });

        var service = CreateService(records, new StubSource(ConfigDomain.StorageConfig, """{"v":1}"""));

        var known = new List<AppliedConfigVersion> { Applied(ConfigDomain.StorageConfig, 3) };
        var bundles = await service.BuildSnapshotAsync(Reg(ComponentType.VMHostAgent), known, CancellationToken.None);

        bundles.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildSnapshot_reflects_updated_source_after_first_use()
    {
        // Regression: a pull must re-evaluate the source. A record created earlier at
        // v1 must be bumped and re-sent when the controller settings later change.
        var existing = new ConfigRecord
        {
            Id = Guid.NewGuid(),
            Domain = ConfigDomain.StorageConfig,
            Scope = "",
            Version = 1,
            Payload = """{"old":true}""",
        };
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        records.Setup(r => r.UpdateAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(records, new StubSource(ConfigDomain.StorageConfig, """{"new":true}"""));

        // Component still holds v1; the source changed, so it must receive v2.
        var known = new List<AppliedConfigVersion> { Applied(ConfigDomain.StorageConfig, 1) };
        var bundles = await service.BuildSnapshotAsync(Reg(ComponentType.VMHostAgent), known, CancellationToken.None);

        bundles.Should().ContainSingle();
        bundles[0].Version.Should().Be(2);
        bundles[0].Payload.Should().Be("""{"new":true}""");
    }

    [Fact]
    public async Task Refresh_creates_record_and_pushes_to_a_component_that_lacks_it()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord?)null);
        records.Setup(r => r.AddAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord record, CancellationToken _) => record);

        var service = CreateService(records, new StubSource(ConfigDomain.StorageConfig, """{"v":1}"""));

        var bundle = await service.RefreshForComponentAsync(
            ConfigDomain.StorageConfig, Reg(ComponentType.VMHostAgent), CancellationToken.None);

        bundle.Should().NotBeNull();
        bundle!.Domain.Should().Be(ConfigDomain.StorageConfig);
        bundle.Version.Should().Be(1);
        bundle.Payload.Should().Be("""{"v":1}""");
        records.Verify(r => r.AddAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_bumps_version_and_pushes_when_payload_changed()
    {
        var existing = new ConfigRecord
        {
            Id = Guid.NewGuid(),
            Domain = ConfigDomain.StorageConfig,
            Scope = "",
            Version = 3,
            Payload = """{"v":"old"}""",
        };
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        records.Setup(r => r.UpdateAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(records, new StubSource(ConfigDomain.StorageConfig, """{"v":"new"}"""));

        // The component holds the old v3; after the bump to v4 it is behind and receives it.
        var applied = new List<AppliedConfigVersion> { Applied(ConfigDomain.StorageConfig, 3) };
        var bundle = await service.RefreshForComponentAsync(
            ConfigDomain.StorageConfig, Reg(ComponentType.VMHostAgent, applied), CancellationToken.None);

        bundle.Should().NotBeNull();
        bundle!.Version.Should().Be(4);
        bundle.Payload.Should().Be("""{"v":"new"}""");
        records.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_returns_null_when_the_component_is_already_current()
    {
        var existing = new ConfigRecord
        {
            Id = Guid.NewGuid(),
            Domain = ConfigDomain.StorageConfig,
            Scope = "",
            Version = 5,
            Payload = """{"v":"same"}""",
        };
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = CreateService(records, new StubSource(ConfigDomain.StorageConfig, """{"v":"same"}"""));

        // Payload unchanged (record stays v5) and the component already applied v5 — nothing to push.
        var applied = new List<AppliedConfigVersion> { Applied(ConfigDomain.StorageConfig, 5) };
        var bundle = await service.RefreshForComponentAsync(
            ConfigDomain.StorageConfig, Reg(ComponentType.VMHostAgent, applied), CancellationToken.None);

        bundle.Should().BeNull();
        records.Verify(r => r.UpdateAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_returns_null_when_no_source_for_domain()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();

        // No source registered for the requested domain.
        var service = CreateService(records);

        var bundle = await service.RefreshForComponentAsync(
            ConfigDomain.StorageConfig, Reg(ComponentType.VMHostAgent), CancellationToken.None);

        bundle.Should().BeNull();
        records.Verify(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Network_component_is_entitled_to_the_OvnCluster_domain()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        var service = CreateService(records);

        service.GetEntitledDomains(ComponentType.Network)
            .Should().Contain(ConfigDomain.OvnCluster);
    }

    [Fact]
    public void GenePool_component_is_entitled_to_the_StorageConfig_domain()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        var service = CreateService(records);

        // The gene pool derives its storage root from the distributed storage config.
        service.GetEntitledDomains(ComponentType.GenePoolAgent)
            .Should().Contain(ConfigDomain.StorageConfig);
    }

    [Fact]
    public async Task GetOutdatedBundles_returns_the_bundle_for_a_domain_the_component_is_behind_on()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigRecord
            {
                Id = Guid.NewGuid(),
                Domain = ConfigDomain.OvnCluster,
                Scope = "",
                Version = 3,
                Payload = """{"chassis":[]}""",
            });

        var service = CreateService(records);

        var applied = new List<AppliedConfigVersion> { Applied(ConfigDomain.OvnCluster, 1) };
        var bundles = await service.GetOutdatedBundlesAsync(
            Reg(ComponentType.Network, applied), CancellationToken.None);

        bundles.Should().ContainSingle();
        bundles[0].Domain.Should().Be(ConfigDomain.OvnCluster);
        bundles[0].Version.Should().Be(3);
        bundles[0].Payload.Should().Be("""{"chassis":[]}""");
    }

    [Fact]
    public async Task GetOutdatedBundles_uses_the_stored_record_without_re_evaluating_the_source()
    {
        // Drift detection must be cheap: it reflects the record the push path already published, not a
        // fresh source build, and never bumps the version. Even with a source that would produce a
        // different (newer) payload, the outdated bundle is the stored v3.
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigRecord
            {
                Id = Guid.NewGuid(), Domain = ConfigDomain.OvnCluster, Scope = "", Version = 3, Payload = "stored",
            });

        var service = CreateService(records, new StubSource(ConfigDomain.OvnCluster, "fresh-would-bump"));

        var applied = new List<AppliedConfigVersion> { Applied(ConfigDomain.OvnCluster, 1) };
        var bundles = await service.GetOutdatedBundlesAsync(
            Reg(ComponentType.Network, applied), CancellationToken.None);

        bundles.Should().ContainSingle();
        bundles[0].Version.Should().Be(3);
        bundles[0].Payload.Should().Be("stored");
        records.Verify(r => r.UpdateAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        records.Verify(r => r.AddAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOutdatedBundles_is_empty_when_the_component_is_current()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigRecord
            {
                Id = Guid.NewGuid(), Domain = ConfigDomain.OvnCluster, Scope = "", Version = 3, Payload = "p",
            });

        var service = CreateService(records);

        var applied = new List<AppliedConfigVersion> { Applied(ConfigDomain.OvnCluster, 3) };
        var bundles = await service.GetOutdatedBundlesAsync(
            Reg(ComponentType.Network, applied), CancellationToken.None);

        bundles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOutdatedBundles_skips_a_domain_with_no_record_yet()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord?)null);

        var service = CreateService(records);

        var bundles = await service.GetOutdatedBundlesAsync(
            Reg(ComponentType.Network), CancellationToken.None);

        bundles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOutdatedBundles_returns_only_the_domains_the_component_is_behind_on()
    {
        // A host agent is entitled to three domains: it is behind on storage, current on
        // network-providers, and no endpoints record exists yet — only the storage bundle is returned.
        var byDomain = new Dictionary<ConfigDomain, ConfigRecord>
        {
            [ConfigDomain.StorageConfig] = new()
                { Id = Guid.NewGuid(), Domain = ConfigDomain.StorageConfig, Scope = "", Version = 5, Payload = "storage" },
            [ConfigDomain.NetworkProviders] = new()
                { Id = Guid.NewGuid(), Domain = ConfigDomain.NetworkProviders, Scope = "", Version = 2, Payload = "network" },
            // No Endpoints record.
        };
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecordSpecs.GetByDomainAndScope spec, CancellationToken _) =>
                byDomain.GetValueOrDefault(spec.Domain));

        var service = CreateService(records);

        var applied = new List<AppliedConfigVersion>
        {
            Applied(ConfigDomain.StorageConfig, 3),   // behind: record is v5
            Applied(ConfigDomain.NetworkProviders, 2),  // current: record is v2
            // Endpoints: neither applied nor a record — skipped.
        };
        var bundles = await service.GetOutdatedBundlesAsync(
            Reg(ComponentType.VMHostAgent, applied), CancellationToken.None);

        bundles.Should().ContainSingle();
        bundles[0].Domain.Should().Be(ConfigDomain.StorageConfig);
        bundles[0].Version.Should().Be(5);
        bundles[0].Payload.Should().Be("storage");
    }

    [Fact]
    public async Task GetOutdatedBundles_for_an_unentitled_component_reads_nothing()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        var service = CreateService(records);

        var bundles = await service.GetOutdatedBundlesAsync(
            Reg(ComponentType.ComputeApi), CancellationToken.None);

        bundles.Should().BeEmpty();
        records.Verify(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOutdatedBundles_forces_a_push_when_the_resolved_scope_changed_even_at_a_lower_version()
    {
        // The component was last distributed the default scope (applied v5) but is now assigned env:edge,
        // whose independent counter is only at v1. A plain version comparison keyed only by domain would
        // wrongly skip it; because applied versions are tracked per (domain, scope), the component's
        // applied version at env:edge is 0, so the lower-numbered scoped record is still pushed. This is
        // the regression guard for the scope-blind version-comparison hazard.
        var registration = new ComponentRegistration
        {
            Id = Guid.NewGuid(),
            ComponentId = Guid.NewGuid(),
            ComponentType = ComponentType.VMHostAgent,
            MachineName = "edge-host",
            InboundQueue = "q",
            Environment = "edge",
        };
        registration.SetAppliedVersion(ConfigDomain.StorageConfig, "", 5);

        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecordSpecs.GetByDomainAndScope spec, CancellationToken _) =>
                spec is { Domain: ConfigDomain.StorageConfig, Scope: "env:edge" }
                    ? new ConfigRecord
                    {
                        Id = Guid.NewGuid(), Domain = ConfigDomain.StorageConfig,
                        Scope = "env:edge", Version = 1, Payload = "edge",
                    }
                    : null);

        var service = new ConfigDistributionService(
            records.Object, [], new ScopedAuthoredStore(ConfigDomain.StorageConfig, "env:edge"), NoOpLock());

        var bundles = await service.GetOutdatedBundlesAsync(registration, CancellationToken.None);

        bundles.Should().ContainSingle();
        bundles[0].Domain.Should().Be(ConfigDomain.StorageConfig);
        bundles[0].Scope.Should().Be("env:edge");
        bundles[0].Version.Should().Be(1);
    }

    /// <summary>Reports an authored value only for the given domain + scope, else none.</summary>
    private sealed class ScopedAuthoredStore(ConfigDomain authoredDomain, string authoredScope) : IAuthoredConfigStore
    {
        public Task<AuthoredConfig?> GetCurrentAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => Task.FromResult(domain == authoredDomain && scope == authoredScope
                ? new AuthoredConfig { Id = Guid.NewGuid(), Domain = domain, Scope = scope, Version = 1, Payload = "x" }
                : null);

        public Task<AuthoredConfig> AddVersionAsync(
            ConfigDomain domain, string scope, string payload, string? author, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<AuthoredConfig>> GetHistoryAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    /// <summary>A config source whose payload the test controls directly.</summary>
    private sealed class StubSource(ConfigDomain domain, string payload) : IConfigSource
    {
        public ConfigDomain Domain => domain;

        public Task<string> BuildPayloadAsync(string scope, CancellationToken cancellationToken) =>
            Task.FromResult(payload);
    }
}
