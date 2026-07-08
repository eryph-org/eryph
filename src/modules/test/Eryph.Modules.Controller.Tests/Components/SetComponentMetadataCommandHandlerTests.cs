using Dbosoft.Rebus.Operations;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.Rebus;
using Eryph.StateDb.Model;
using Moq;
using Rebus.Bus;
using Rebus.Bus.Advanced;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the component-metadata operation handler: assigning to a registered component completes
/// the operation and triggers a refresh of its authorable domains (the new scope can move it onto a
/// different authored value); targeting an unknown component fails it.
/// <see cref="IComponentRegistryService"/> is internal and cannot be proxied by Moq, so it is
/// hand-stubbed.
/// </summary>
public class SetComponentMetadataCommandHandlerTests
{
    private readonly Mock<ITaskMessaging> _messaging = new();
    private readonly Mock<IBus> _bus = new();
    private readonly Mock<IRoutingApi> _routing = new();

    public SetComponentMetadataCommandHandlerTests()
    {
        var advanced = new Mock<IAdvancedApi>();
        advanced.Setup(a => a.Routing).Returns(_routing.Object);
        _bus.Setup(b => b.Advanced).Returns(advanced.Object);
    }

    private OperationTask<SetComponentMetadataCommand> Op(Guid componentId) =>
        new(new SetComponentMetadataCommand
            {
                ComponentId = componentId,
                Environment = "prod",
                Tags = new Dictionary<string, string> { ["rack"] = "r1" },
            },
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

    // Verified via CompleteTask (FailTask has optional args and cannot appear in a Moq expression
    // tree): the handler either completes or fails, so "not completed" pins the failure path.
    private void VerifyCompleted(Times times) =>
        _messaging.Verify(m => m.CompleteTask(
            It.IsAny<IOperationTaskMessage>(), It.IsAny<IDictionary<string, string>?>()), times);

    [Fact]
    public async Task Assigning_to_a_registered_component_completes_and_refreshes_its_domains()
    {
        var componentId = Guid.NewGuid();
        var registry = new StubRegistry(found: true)
        {
            Registration = new ComponentRegistration
            {
                ComponentId = componentId,
                ComponentType = ComponentType.VMHostAgent,
                MachineName = "host1",
                InboundQueue = "q",
            },
        };
        var handler = new SetComponentMetadataCommandHandler(_bus.Object, registry, _messaging.Object);

        await handler.Handle(Op(componentId));

        registry.LastComponentId.Should().Be(componentId);
        registry.LastEnvironment.Should().Be("prod");
        registry.LastTags.Should().ContainKey("rack");
        VerifyCompleted(Times.Once());

        // The agent's one authorable domain is re-distributed; the system-derived ones are not.
        _routing.Verify(r => r.Send(
            QueueNames.Controllers,
            It.Is<RefreshConfigDomainCommand>(c => c.Domain == ConfigDomain.StorageConfig),
            It.IsAny<IDictionary<string, string>?>()), Times.Once());
        _routing.Verify(r => r.Send(
            QueueNames.Controllers,
            It.Is<RefreshConfigDomainCommand>(c => c.Domain == ConfigDomain.Endpoints),
            It.IsAny<IDictionary<string, string>?>()), Times.Never());
    }

    [Fact]
    public async Task Targeting_an_unknown_component_fails_the_operation()
    {
        var handler = new SetComponentMetadataCommandHandler(
            _bus.Object, new StubRegistry(found: false), _messaging.Object);

        await handler.Handle(Op(Guid.NewGuid()));

        // The unknown component fails the task, so it is never completed and nothing is refreshed.
        VerifyCompleted(Times.Never());
        _routing.Verify(r => r.Send(
            It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDictionary<string, string>?>()), Times.Never());
    }

    private sealed class StubRegistry(bool found) : IComponentRegistryService
    {
        public Guid LastComponentId { get; private set; }
        public string? LastEnvironment { get; private set; }
        public IReadOnlyDictionary<string, string> LastTags { get; private set; } = new Dictionary<string, string>();
        public ComponentRegistration? Registration { get; init; }

        public Task<bool> SetMetadataAsync(
            Guid componentId, string? environment, IReadOnlyDictionary<string, string> tags,
            CancellationToken cancellationToken)
        {
            LastComponentId = componentId;
            LastEnvironment = environment;
            LastTags = tags;
            return Task.FromResult(found);
        }

        public Task<ComponentRegistration?> GetAsync(Guid componentId, CancellationToken cancellationToken)
            => Task.FromResult(Registration);

        public Task<ComponentRegistration> UpsertAsync(RegisterComponentCommand command, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ComponentRegistration?> RecordHeartbeatAsync(
            Guid componentId, Guid instanceId, IReadOnlyDictionary<ConfigDomain, long> appliedConfigVersions,
            CancellationToken ct) => throw new NotSupportedException();

        public Task RecordAppliedAsync(Guid componentId, ConfigDomain domain, long version, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<bool> DeregisterAsync(Guid componentId, Guid instanceId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<bool> RemoveRegistrationAsync(Guid componentId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ComponentRegistration>> GetActiveAsync(CancellationToken ct)
            => throw new NotSupportedException();
    }
}
