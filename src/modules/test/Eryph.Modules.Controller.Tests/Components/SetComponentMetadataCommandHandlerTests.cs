using Dbosoft.Rebus.Operations;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb.Model;
using Moq;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the component-metadata operation handler: assigning to a registered component completes
/// the operation; targeting an unknown component fails it. <see cref="IComponentRegistryService"/> is
/// internal and cannot be proxied by Moq, so it is hand-stubbed.
/// </summary>
public class SetComponentMetadataCommandHandlerTests
{
    private readonly Mock<ITaskMessaging> _messaging = new();

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
    public async Task Assigning_to_a_registered_component_completes_the_operation()
    {
        var registry = new StubRegistry(found: true);
        var handler = new SetComponentMetadataCommandHandler(registry, _messaging.Object);
        var componentId = Guid.NewGuid();

        await handler.Handle(Op(componentId));

        registry.LastComponentId.Should().Be(componentId);
        registry.LastEnvironment.Should().Be("prod");
        registry.LastTags.Should().ContainKey("rack");
        VerifyCompleted(Times.Once());
    }

    [Fact]
    public async Task Targeting_an_unknown_component_fails_the_operation()
    {
        var handler = new SetComponentMetadataCommandHandler(new StubRegistry(found: false), _messaging.Object);

        await handler.Handle(Op(Guid.NewGuid()));

        // The unknown component fails the task, so it is never completed.
        VerifyCompleted(Times.Never());
    }

    private sealed class StubRegistry(bool found) : IComponentRegistryService
    {
        public Guid LastComponentId { get; private set; }
        public string? LastEnvironment { get; private set; }
        public IReadOnlyDictionary<string, string> LastTags { get; private set; } = new Dictionary<string, string>();

        public Task<bool> SetMetadataAsync(
            Guid componentId, string? environment, IReadOnlyDictionary<string, string> tags,
            CancellationToken cancellationToken)
        {
            LastComponentId = componentId;
            LastEnvironment = environment;
            LastTags = tags;
            return Task.FromResult(found);
        }

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
