using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Moq;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the controller's component catalog: registration upsert (insert/update),
/// advertised-endpoint persistence, heartbeat liveness/state reconciliation, and the
/// monotonic applied-config tracking.
/// </summary>
public class ComponentRegistryServiceTests
{
    private static (ComponentRegistryService Service, Mock<IStateStoreRepository<ComponentRegistration>> Repo) Create(
        ComponentRegistration? existing = null)
    {
        var repo = new Mock<IStateStoreRepository<ComponentRegistration>>();
        repo.Setup(r => r.GetBySpecAsync(
                It.IsAny<ComponentRegistrationSpecs.GetByComponentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.AddAsync(It.IsAny<ComponentRegistration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComponentRegistration r, CancellationToken _) => r);
        repo.Setup(r => r.UpdateAsync(It.IsAny<ComponentRegistration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return (new ComponentRegistryService(repo.Object), repo);
    }

    private static RegisterComponentCommand RegisterCommand(
        Guid componentId,
        ComponentType type = ComponentType.Identity,
        Dictionary<string, string>? advertised = null,
        Dictionary<ConfigDomain, long>? known = null) =>
        new()
        {
            ComponentId = componentId,
            ComponentType = type,
            InstanceId = Guid.NewGuid(),
            MachineName = "host.example.test",
            Version = "1.0",
            InboundQueue = "eryph.identity.host",
            KnownConfigVersions = known ?? new(),
            AdvertisedEndpoints = advertised ?? new(),
        };

    [Fact]
    public async Task Upsert_inserts_new_registration_with_advertised_endpoints()
    {
        var (service, repo) = Create(existing: null);
        var componentId = Guid.NewGuid();

        var result = await service.UpsertAsync(
            RegisterCommand(componentId, ComponentType.Identity,
                advertised: new() { ["identity"] = "https://host/identity" },
                known: new() { [ConfigDomain.Endpoints] = 2 }),
            CancellationToken.None);

        repo.Verify(r => r.AddAsync(It.IsAny<ComponentRegistration>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.UpdateAsync(It.IsAny<ComponentRegistration>(), It.IsAny<CancellationToken>()), Times.Never);
        result.ComponentId.Should().Be(componentId);
        result.Status.Should().Be(ComponentRegistrationStatus.Active);
        result.AdvertisedEndpoints.Should().ContainKey("identity").WhoseValue.Should().Be("https://host/identity");
        result.AppliedConfigVersions.Should().ContainKey(ConfigDomain.Endpoints).WhoseValue.Should().Be(2);
    }

    [Fact]
    public async Task Upsert_updates_existing_registration_and_replaces_advertised_endpoints()
    {
        var componentId = Guid.NewGuid();
        var existing = new ComponentRegistration
        {
            Id = Guid.NewGuid(),
            ComponentId = componentId,
            ComponentType = ComponentType.Identity,
            InstanceId = Guid.NewGuid(),
            MachineName = "old",
            InboundQueue = "old.queue",
            Status = ComponentRegistrationStatus.Stale,
            AdvertisedEndpoints = new() { ["identity"] = "https://old/identity" },
        };
        var (service, repo) = Create(existing);

        var result = await service.UpsertAsync(
            RegisterCommand(componentId, ComponentType.Identity,
                advertised: new() { ["identity"] = "https://new/identity" }),
            CancellationToken.None);

        repo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AddAsync(It.IsAny<ComponentRegistration>(), It.IsAny<CancellationToken>()), Times.Never);
        result.Status.Should().Be(ComponentRegistrationStatus.Active);
        result.InboundQueue.Should().Be("eryph.identity.host");
        result.AdvertisedEndpoints["identity"].Should().Be("https://new/identity");
    }

    [Fact]
    public async Task Upsert_merges_known_versions_taking_the_higher_value()
    {
        var componentId = Guid.NewGuid();
        var existing = new ComponentRegistration
        {
            Id = Guid.NewGuid(),
            ComponentId = componentId,
            ComponentType = ComponentType.VMHostAgent,
            MachineName = "host",
            InboundQueue = "q",
            AppliedConfigVersions = new() { [ConfigDomain.PlacementConfig] = 5, [ConfigDomain.Endpoints] = 4 },
        };
        var (service, _) = Create(existing);

        var result = await service.UpsertAsync(
            RegisterCommand(componentId, ComponentType.VMHostAgent,
                known: new() { [ConfigDomain.PlacementConfig] = 3, [ConfigDomain.Endpoints] = 9 }),
            CancellationToken.None);

        // Existing higher value is kept; reported higher value wins.
        result.AppliedConfigVersions[ConfigDomain.PlacementConfig].Should().Be(5);
        result.AppliedConfigVersions[ConfigDomain.Endpoints].Should().Be(9);
    }

    [Fact]
    public async Task RecordHeartbeat_overwrites_applied_versions_and_marks_active()
    {
        var componentId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var existing = new ComponentRegistration
        {
            Id = Guid.NewGuid(),
            ComponentId = componentId,
            ComponentType = ComponentType.VMHostAgent,
            InstanceId = instanceId,
            MachineName = "host",
            InboundQueue = "q",
            Status = ComponentRegistrationStatus.Stale,
            AppliedConfigVersions = new() { [ConfigDomain.PlacementConfig] = 5 },
        };
        var (service, repo) = Create(existing);

        // A restart reports an empty/reset applied set; the heartbeat from the registered
        // instance must reflect it verbatim.
        await service.RecordHeartbeatAsync(
            componentId, instanceId, new Dictionary<ConfigDomain, long>(), CancellationToken.None);

        repo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        existing.Status.Should().Be(ComponentRegistrationStatus.Active);
        existing.AppliedConfigVersions.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordHeartbeat_from_a_stale_instance_does_not_revert_the_catalog()
    {
        var componentId = Guid.NewGuid();
        var currentInstance = Guid.NewGuid();
        var staleInstance = Guid.NewGuid();
        var existing = new ComponentRegistration
        {
            Id = Guid.NewGuid(),
            ComponentId = componentId,
            ComponentType = ComponentType.VMHostAgent,
            InstanceId = currentInstance,
            MachineName = "host",
            InboundQueue = "q",
            Status = ComponentRegistrationStatus.Active,
            AppliedConfigVersions = new() { [ConfigDomain.PlacementConfig] = 5, [ConfigDomain.Endpoints] = 3 },
        };
        var (service, repo) = Create(existing);

        // A delayed heartbeat from a previous process instance (e.g. reordered on the broker)
        // must be ignored so it cannot revert InstanceId or applied-config state.
        await service.RecordHeartbeatAsync(
            componentId, staleInstance, new Dictionary<ConfigDomain, long>(), CancellationToken.None);

        repo.Verify(r => r.UpdateAsync(It.IsAny<ComponentRegistration>(), It.IsAny<CancellationToken>()), Times.Never);
        existing.InstanceId.Should().Be(currentInstance);
        existing.AppliedConfigVersions.Should().HaveCount(2);
    }

    [Fact]
    public async Task RecordHeartbeat_is_a_noop_when_not_registered()
    {
        var (service, repo) = Create(existing: null);

        await service.RecordHeartbeatAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            new Dictionary<ConfigDomain, long>(), CancellationToken.None);

        repo.Verify(r => r.UpdateAsync(It.IsAny<ComponentRegistration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordApplied_is_monotonic_per_domain()
    {
        var componentId = Guid.NewGuid();
        var existing = new ComponentRegistration
        {
            Id = Guid.NewGuid(),
            ComponentId = componentId,
            ComponentType = ComponentType.VMHostAgent,
            MachineName = "host",
            InboundQueue = "q",
            AppliedConfigVersions = new() { [ConfigDomain.NetworkProviders] = 7 },
        };
        var (service, repo) = Create(existing);

        // Older acknowledgement is ignored (no regression, no write).
        await service.RecordAppliedAsync(componentId, ConfigDomain.NetworkProviders, 5, CancellationToken.None);
        existing.AppliedConfigVersions[ConfigDomain.NetworkProviders].Should().Be(7);
        repo.Verify(r => r.UpdateAsync(It.IsAny<ComponentRegistration>(), It.IsAny<CancellationToken>()), Times.Never);

        // Newer acknowledgement advances and persists.
        await service.RecordAppliedAsync(componentId, ConfigDomain.NetworkProviders, 9, CancellationToken.None);
        existing.AppliedConfigVersions[ConfigDomain.NetworkProviders].Should().Be(9);
        repo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetActive_excludes_components_past_the_heartbeat_timeout()
    {
        var (service, repo) = Create();
        var fresh = ActiveComponent("fresh.example", DateTimeOffset.UtcNow);
        // One heartbeat-timeout-plus-a-bit since the last heartbeat: aged out even though still Active.
        var stale = ActiveComponent("stale.example",
            DateTimeOffset.UtcNow - ComponentRegistrationDefaults.HeartbeatTimeout - TimeSpan.FromSeconds(1));
        repo.Setup(r => r.ListAsync(
                It.IsAny<ComponentRegistrationSpecs.GetActive>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ComponentRegistration> { fresh, stale });

        var result = await service.GetActiveAsync(CancellationToken.None);

        result.Should().ContainSingle().Which.Should().BeSameAs(fresh);
    }

    private static ComponentRegistration ActiveComponent(string machineName, DateTimeOffset lastHeartbeat) =>
        new()
        {
            Id = Guid.NewGuid(),
            ComponentId = Guid.NewGuid(),
            ComponentType = ComponentType.Network,
            MachineName = machineName,
            InboundQueue = "q",
            Status = ComponentRegistrationStatus.Active,
            LastHeartbeat = lastHeartbeat,
        };
}
