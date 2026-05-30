using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Eryph.Core;
using Eryph.Core.Settings;
using Eryph.DistributedLock;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using LanguageExt.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the config-distribution model independently of component registration:
/// the snapshot is built from the request (component type + known versions) and the
/// controller settings, never from a <c>ComponentRegistration</c> row.
/// </summary>
public class ConfigDistributionServiceTests
{
    private static ConfigDistributionService CreateService(
        ControllerSettings settings,
        Mock<IStateStoreRepository<ConfigRecord>> records)
    {
        var settingsManager = new Mock<IControllerSettingsManager>();
        settingsManager.Setup(m => m.GetCurrentConfiguration())
            .Returns(RightAsync<Error, ControllerSettings>(settings));

        var source = new PlacementConfigSource(settingsManager.Object, NullLogger<PlacementConfigSource>.Instance);
        return new ConfigDistributionService(records.Object, new IConfigSource[] { source }, NoOpLock());
    }

    // The distributed lock only serializes concurrent access; for single-threaded unit tests a
    // no-op holder (AcquireLock returns a completed ValueTask by default) is sufficient.
    private static IDistributedLockScopeHolder NoOpLock() =>
        new Mock<IDistributedLockScopeHolder>().Object;

    /// <summary>A config source whose payload the test controls directly.</summary>
    private sealed class StubSource(ConfigDomain domain, string payload) : IConfigSource
    {
        public ConfigDomain Domain => domain;

        public Task<string> BuildPayloadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(payload);
    }

    private static ConfigDistributionService CreateService(
        Mock<IStateStoreRepository<ConfigRecord>> records,
        params IConfigSource[] sources) =>
        new(records.Object, sources, NoOpLock());

    [Fact]
    public async Task BuildSnapshot_for_entitled_component_returns_placement_bundle_from_settings()
    {
        var settings = new ControllerSettings
        {
            Placement = new PlacementConfig { Datastores = ["ds1"], Environments = ["env1"] },
        };

        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomain>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord?)null);
        records.Setup(r => r.AddAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord record, CancellationToken _) => record);

        var service = CreateService(settings, records);

        var bundles = await service.BuildSnapshotAsync(
            ComponentType.VMHostAgent, new Dictionary<ConfigDomain, long>(), CancellationToken.None);

        bundles.Should().ContainSingle();
        bundles[0].Domain.Should().Be(ConfigDomain.PlacementConfig);
        bundles[0].Version.Should().Be(1);

        var payload = JsonSerializer.Deserialize<PlacementConfig>(bundles[0].Payload)!;
        payload.Datastores.Should().BeEquivalentTo(new[] { "ds1" });
        payload.Environments.Should().BeEquivalentTo(new[] { "env1" });
    }

    [Fact]
    public async Task BuildSnapshot_returns_every_entitled_domain_that_has_a_source()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomain>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord?)null);
        records.Setup(r => r.AddAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord record, CancellationToken _) => record);

        // The host agent is entitled to both placement and network-provider config.
        var service = CreateService(records,
            new StubSource(ConfigDomain.PlacementConfig, """{"p":1}"""),
            new StubSource(ConfigDomain.NetworkProviders, "network_providers: []"));

        var bundles = await service.BuildSnapshotAsync(
            ComponentType.VMHostAgent, new Dictionary<ConfigDomain, long>(), CancellationToken.None);

        bundles.Select(b => b.Domain).Should().BeEquivalentTo(
            [ConfigDomain.PlacementConfig, ConfigDomain.NetworkProviders]);
    }

    [Fact]
    public async Task BuildSnapshot_for_unentitled_component_is_empty()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        var service = CreateService(new ControllerSettings(), records);

        var bundles = await service.BuildSnapshotAsync(
            ComponentType.GenePoolAgent, new Dictionary<ConfigDomain, long>(), CancellationToken.None);

        bundles.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildSnapshot_omits_domain_the_component_already_has_at_current_version()
    {
        // The stored record matches the source payload, so re-evaluation does not
        // bump the version; the component already holds that version, so nothing is sent.
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomain>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigRecord
            {
                Id = Guid.NewGuid(),
                Domain = ConfigDomain.PlacementConfig,
                Version = 3,
                Payload = """{"v":1}""",
            });

        var service = CreateService(records, new StubSource(ConfigDomain.PlacementConfig, """{"v":1}"""));

        var known = new Dictionary<ConfigDomain, long> { [ConfigDomain.PlacementConfig] = 3 };
        var bundles = await service.BuildSnapshotAsync(ComponentType.VMHostAgent, known, CancellationToken.None);

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
            Domain = ConfigDomain.PlacementConfig,
            Version = 1,
            Payload = """{"old":true}""",
        };
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomain>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        records.Setup(r => r.UpdateAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(records, new StubSource(ConfigDomain.PlacementConfig, """{"new":true}"""));

        // Component still holds v1; the source changed, so it must receive v2.
        var known = new Dictionary<ConfigDomain, long> { [ConfigDomain.PlacementConfig] = 1 };
        var bundles = await service.BuildSnapshotAsync(ComponentType.VMHostAgent, known, CancellationToken.None);

        bundles.Should().ContainSingle();
        bundles[0].Version.Should().Be(2);
        bundles[0].Payload.Should().Be("""{"new":true}""");
    }

    [Fact]
    public async Task Refresh_creates_record_at_version_1_on_first_use()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomain>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord?)null);
        records.Setup(r => r.AddAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigRecord record, CancellationToken _) => record);

        var service = CreateService(records, new StubSource(ConfigDomain.PlacementConfig, """{"v":1}"""));

        var bundle = await service.RefreshAsync(ConfigDomain.PlacementConfig, CancellationToken.None);

        bundle.Should().NotBeNull();
        bundle!.Domain.Should().Be(ConfigDomain.PlacementConfig);
        bundle.Version.Should().Be(1);
        bundle.Payload.Should().Be("""{"v":1}""");
        records.Verify(r => r.AddAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_bumps_version_when_payload_changed()
    {
        var existing = new ConfigRecord
        {
            Id = Guid.NewGuid(),
            Domain = ConfigDomain.PlacementConfig,
            Version = 3,
            Payload = """{"v":"old"}""",
        };
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomain>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        records.Setup(r => r.UpdateAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(records, new StubSource(ConfigDomain.PlacementConfig, """{"v":"new"}"""));

        var bundle = await service.RefreshAsync(ConfigDomain.PlacementConfig, CancellationToken.None);

        bundle.Should().NotBeNull();
        bundle!.Version.Should().Be(4);
        bundle.Payload.Should().Be("""{"v":"new"}""");
        records.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_returns_null_when_payload_unchanged()
    {
        var existing = new ConfigRecord
        {
            Id = Guid.NewGuid(),
            Domain = ConfigDomain.PlacementConfig,
            Version = 5,
            Payload = """{"v":"same"}""",
        };
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomain>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var service = CreateService(records, new StubSource(ConfigDomain.PlacementConfig, """{"v":"same"}"""));

        var bundle = await service.RefreshAsync(ConfigDomain.PlacementConfig, CancellationToken.None);

        bundle.Should().BeNull();
        records.Verify(r => r.UpdateAsync(It.IsAny<ConfigRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_returns_null_when_no_source_for_domain()
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();

        // No source registered for the requested domain.
        var service = CreateService(records);

        var bundle = await service.RefreshAsync(ConfigDomain.PlacementConfig, CancellationToken.None);

        bundle.Should().BeNull();
        records.Verify(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomain>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
