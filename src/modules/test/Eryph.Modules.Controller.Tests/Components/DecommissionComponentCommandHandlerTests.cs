using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eryph.Modules.Controller.Tests.Components;

public class DecommissionComponentCommandHandlerTests
{
    private static readonly Guid ComponentId = Guid.NewGuid();

    [Fact]
    public async Task Decommission_removes_the_broker_user_and_the_registration()
    {
        var broker = new RecordingBrokerProvisioner();
        var registry = new StubRegistry(true);
        var handler = new DecommissionComponentCommandHandler(
            registry, [broker], NullLogger<DecommissionComponentCommandHandler>.Instance);

        await handler.Handle(new DecommissionComponentCommand { ComponentId = ComponentId });

        broker.Removed.Should().ContainSingle().Which.Should().Be(ComponentId);
        registry.Revoked.Should().ContainSingle().Which.Should().Be(ComponentId);
    }

    [Fact]
    public async Task Decommission_removes_the_broker_user_even_when_no_registration_exists()
    {
        // A component that already aged out of the catalog must still be revocable: the broker-user
        // deletion is the actual cutoff and must happen regardless of whether a row was found.
        var broker = new RecordingBrokerProvisioner();
        var registry = new StubRegistry(false);
        var handler = new DecommissionComponentCommandHandler(
            registry, [broker], NullLogger<DecommissionComponentCommandHandler>.Instance);

        await handler.Handle(new DecommissionComponentCommand { ComponentId = ComponentId });

        broker.Removed.Should().ContainSingle().Which.Should().Be(ComponentId);
    }

    [Fact]
    public async Task Decommission_with_no_broker_only_removes_the_registration()
    {
        // eryph-zero (no managed broker) resolves an empty provisioner collection; decommission then
        // just removes the registration without any broker call.
        var registry = new StubRegistry(true);
        var handler = new DecommissionComponentCommandHandler(
            registry, [], NullLogger<DecommissionComponentCommandHandler>.Instance);

        await handler.Handle(new DecommissionComponentCommand { ComponentId = ComponentId });

        registry.Revoked.Should().ContainSingle().Which.Should().Be(ComponentId);
    }

    private sealed class RecordingBrokerProvisioner : IComponentBrokerProvisioner
    {
        public List<Guid> Removed { get; } = [];

        public Task EnsureComponentAsync(Guid componentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Decommission does not ensure users.");

        public Task RemoveComponentAsync(Guid componentId, CancellationToken cancellationToken = default)
        {
            Removed.Add(componentId);
            return Task.CompletedTask;
        }
    }

    private sealed class StubRegistry(bool revokeResult) : IComponentRegistryService
    {
        public List<Guid> Revoked { get; } = [];

        public Task<bool> RemoveRegistrationAsync(Guid componentId, CancellationToken cancellationToken)
        {
            Revoked.Add(componentId);
            return Task.FromResult(revokeResult);
        }

        public Task<ComponentRegistration> UpsertAsync(RegisterComponentCommand command,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task RecordHeartbeatAsync(Guid componentId, Guid instanceId,
            IReadOnlyDictionary<ConfigDomain, long> appliedConfigVersions, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task RecordAppliedAsync(Guid componentId, ConfigDomain domain, long version,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> DeregisterAsync(Guid componentId, Guid instanceId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ComponentRegistration>> GetActiveAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
