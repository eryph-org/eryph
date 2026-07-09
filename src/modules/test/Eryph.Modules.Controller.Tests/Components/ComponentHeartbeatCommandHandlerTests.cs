using Eryph.DistributedLock;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Moq;
using Rebus.Bus;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies heartbeat drift reconciliation: a component reporting an applied-config state behind the
/// authoritative records is re-pushed the newer bundles on its own inbound queue; a current component
/// (or a stale/unregistered heartbeat) is not. <see cref="IComponentRegistryService"/> is internal and
/// cannot be proxied by Moq, so it is hand-stubbed like elsewhere in these tests.
/// </summary>
public class ComponentHeartbeatCommandHandlerTests
{
    private static readonly Guid ComponentId = Guid.NewGuid();
    private static readonly Guid InstanceId = Guid.NewGuid();

    private static ConfigDistributionService Distribution(Mock<IStateStoreRepository<ConfigRecord>> records) =>
        new(records.Object, [], new EmptyAuthoredStore(), new Mock<IDistributedLockScopeHolder>().Object);

    // Nothing authored, so every domain resolves the default scope. IAuthoredConfigStore is internal
    // and cannot be proxied by Moq, so it is hand-stubbed.
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

    private static ComponentHeartbeatCommand Heartbeat(long appliedOvnClusterVersion) =>
        new()
        {
            ComponentId = ComponentId,
            InstanceId = InstanceId,
            AppliedConfigVersions =
                [new AppliedConfigVersion { Domain = ConfigDomain.OvnCluster, Scope = "", Version = appliedOvnClusterVersion }],
        };

    private static ComponentRegistration NetworkRegistration()
    {
        var registration = new ComponentRegistration
        {
            Id = Guid.NewGuid(),
            ComponentId = ComponentId,
            ComponentType = ComponentType.Network,
            InstanceId = InstanceId,
            MachineName = "net",
            InboundQueue = "net-inbound",
        };
        registration.SetAppliedVersion(ConfigDomain.OvnCluster, "", 1);
        return registration;
    }

    private static Mock<IStateStoreRepository<ConfigRecord>> RecordsAt(long ovnClusterVersion)
    {
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        records.Setup(r => r.GetBySpecAsync(It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigRecord
            {
                Id = Guid.NewGuid(), Domain = ConfigDomain.OvnCluster, Scope = "", Version = ovnClusterVersion, Payload = "p",
            });
        return records;
    }

    [Fact]
    public async Task Behind_component_is_re_pushed_the_outdated_bundle_to_its_inbound_queue()
    {
        var registry = new StubRegistry(NetworkRegistration());
        var records = RecordsAt(5);
        var bus = new Mock<IBus> { DefaultValue = DefaultValue.Mock };
        var handler = new ComponentHeartbeatCommandHandler(bus.Object, registry, Distribution(records));

        // The component reports OvnCluster v1 but the record is at v5 — it missed a push.
        await handler.Handle(Heartbeat(1));

        Mock.Get(bus.Object.Advanced.Routing).Verify(r => r.Send(
                "net-inbound",
                It.Is<ConfigSnapshotCommand>(c =>
                    c.ComponentId == ComponentId
                    && c.Bundles.Count == 1
                    && c.Bundles[0].Domain == ConfigDomain.OvnCluster
                    && c.Bundles[0].Version == 5
                    && c.Bundles[0].Payload == "p"),
                It.IsAny<IDictionary<string, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Behind_component_is_re_pushed_only_the_domains_it_is_behind_on()
    {
        // A host agent (three entitled domains) behind on placement but current on network-providers is
        // re-pushed exactly the placement bundle — the multi-domain partial-drift case, asserted through
        // the handler's actual bus send.
        var registration = new ComponentRegistration
        {
            Id = Guid.NewGuid(),
            ComponentId = ComponentId,
            ComponentType = ComponentType.VMHostAgent,
            InstanceId = InstanceId,
            MachineName = "host",
            InboundQueue = "host-inbound",
        };
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

        var registry = new StubRegistry(registration);
        var bus = new Mock<IBus> { DefaultValue = DefaultValue.Mock };
        var handler = new ComponentHeartbeatCommandHandler(bus.Object, registry, Distribution(records));

        await handler.Handle(new ComponentHeartbeatCommand
        {
            ComponentId = ComponentId,
            InstanceId = InstanceId,
            AppliedConfigVersions =
            [
                new AppliedConfigVersion { Domain = ConfigDomain.StorageConfig, Scope = "", Version = 3 },   // behind: record is v5
                new AppliedConfigVersion { Domain = ConfigDomain.NetworkProviders, Scope = "", Version = 2 },  // current: record is v2
            ],
        });

        Mock.Get(bus.Object.Advanced.Routing).Verify(r => r.Send(
                "host-inbound",
                It.Is<ConfigSnapshotCommand>(c =>
                    c.Bundles.Count == 1
                    && c.Bundles[0].Domain == ConfigDomain.StorageConfig
                    && c.Bundles[0].Version == 5),
                It.IsAny<IDictionary<string, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Current_component_is_not_re_pushed_anything()
    {
        var registry = new StubRegistry(NetworkRegistration());
        var records = RecordsAt(5);
        var bus = new Mock<IBus> { DefaultValue = DefaultValue.Mock };
        var handler = new ComponentHeartbeatCommandHandler(bus.Object, registry, Distribution(records));

        // The component already holds v5.
        await handler.Handle(Heartbeat(5));

        Mock.Get(bus.Object.Advanced.Routing).Verify(r => r.Send(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task Stale_or_unregistered_heartbeat_reconciles_nothing()
    {
        // RecordHeartbeatAsync returns null for an unregistered component or a superseded instance;
        // the handler must then neither read records nor push.
        var registry = new StubRegistry(null);
        var records = new Mock<IStateStoreRepository<ConfigRecord>>();
        var bus = new Mock<IBus> { DefaultValue = DefaultValue.Mock };
        var handler = new ComponentHeartbeatCommandHandler(bus.Object, registry, Distribution(records));

        await handler.Handle(Heartbeat(1));

        records.Verify(r => r.GetBySpecAsync(
            It.IsAny<ConfigRecordSpecs.GetByDomainAndScope>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Get(bus.Object.Advanced.Routing).Verify(r => r.Send(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Returns <paramref name="heartbeatResult"/> from <see cref="RecordHeartbeatAsync"/> (the recorded
    /// registration, or null for an unregistered/superseded heartbeat). Only that member is exercised.
    /// </summary>
    private sealed class StubRegistry(ComponentRegistration? heartbeatResult) : IComponentRegistryService
    {
        public Task<bool> SetMetadataAsync(
            Guid componentId, string? environment, IReadOnlyDictionary<string, string?>? tags,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ComponentRegistration?> RecordHeartbeatAsync(Guid componentId, Guid instanceId,
            IReadOnlyList<AppliedConfigVersion> appliedConfigVersions, CancellationToken cancellationToken)
        {
            // Mirror the real service: the recorded registration reflects the heartbeat's applied state,
            // which is what the handler reconciles against.
            heartbeatResult?.SetAppliedVersions(appliedConfigVersions);
            return Task.FromResult(heartbeatResult);
        }

        public Task<ComponentRegistration> UpsertAsync(RegisterComponentCommand command,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task RecordAppliedAsync(Guid componentId, ConfigDomain domain, string scope, long version,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> DeregisterAsync(Guid componentId, Guid instanceId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> RemoveRegistrationAsync(Guid componentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ComponentRegistration?> GetAsync(Guid componentId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ComponentRegistration>> GetActiveAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
