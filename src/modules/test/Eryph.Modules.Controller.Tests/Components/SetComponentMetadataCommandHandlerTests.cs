using Ardalis.Specification;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.Rebus;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
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
    private readonly Mock<IStateStoreRepository<Site>> _siteRepository = new();

    public SetComponentMetadataCommandHandlerTests()
    {
        var advanced = new Mock<IAdvancedApi>();
        advanced.Setup(a => a.Routing).Returns(_routing.Object);
        _bus.Setup(b => b.Advanced).Returns(advanced.Object);
    }

    private OperationTask<SetComponentMetadataCommand> Op(Guid componentId, string? site = null) =>
        new(new SetComponentMetadataCommand
            {
                ComponentId = componentId,
                Environment = "prod",
                Site = site,
                Tags = new Dictionary<string, string?> { ["rack"] = "r1" },
            },
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

    /// <summary>
    /// The only site that exists. The specification is evaluated in memory, so a lookup for any
    /// other name resolves to nothing — exactly as it would against the database.
    /// </summary>
    private void ArrangeSite(string name, Guid id)
    {
        var site = new Site { Id = id, Name = name };
        _siteRepository
            .Setup(r => r.GetBySpecAsync(
                It.IsAny<SiteSpecs.GetByName>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SiteSpecs.GetByName spec, CancellationToken _) =>
                spec.Evaluate([site]).FirstOrDefault());
    }

    [Fact]
    public async Task Site_is_resolved_by_name_and_assigned()
    {
        var siteId = Guid.NewGuid();
        ArrangeSite("berlin", siteId);
        var registry = new StubRegistry(found: true)
        {
            Registration = new ComponentRegistration
            {
                ComponentId = Guid.NewGuid(),
                ComponentType = ComponentType.VMHostAgent,
                MachineName = "host1",
                InboundQueue = "q",
            },
        };
        var handler = new SetComponentMetadataCommandHandler(
            _bus.Object, registry, _siteRepository.Object, _messaging.Object);

        await handler.Handle(Op(Guid.NewGuid(), site: "berlin"));

        registry.LastSiteId.Should().Be(siteId);
        VerifyCompleted(Times.Once());
    }

    [Fact]
    public async Task An_unknown_site_is_rejected_and_nothing_is_assigned()
    {
        // An unresolvable site would otherwise remove the host from all placement and
        // storage-agent resolution with nothing pointing at the cause.
        ArrangeSite("berlin", Guid.NewGuid());
        var registry = new StubRegistry(found: true);
        var handler = new SetComponentMetadataCommandHandler(
            _bus.Object, registry, _siteRepository.Object, _messaging.Object);

        await handler.Handle(Op(Guid.NewGuid(), site: "munich"));

        registry.LastComponentId.Should().Be(Guid.Empty);
        VerifyCompleted(Times.Never());
    }

    [Fact]
    public async Task An_omitted_site_leaves_the_site_unchanged()
    {
        var registry = new StubRegistry(found: true)
        {
            Registration = new ComponentRegistration
            {
                ComponentId = Guid.NewGuid(),
                ComponentType = ComponentType.VMHostAgent,
                MachineName = "host1",
                InboundQueue = "q",
            },
        };
        var handler = new SetComponentMetadataCommandHandler(
            _bus.Object, registry, _siteRepository.Object, _messaging.Object);

        await handler.Handle(Op(Guid.NewGuid()));

        registry.LastSiteId.Should().BeNull();
        VerifyCompleted(Times.Once());
    }

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
        var handler = new SetComponentMetadataCommandHandler(_bus.Object, registry, _siteRepository.Object, _messaging.Object);

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
            _bus.Object, new StubRegistry(found: false), _siteRepository.Object, _messaging.Object);

        await handler.Handle(Op(Guid.NewGuid()));

        // The unknown component fails the task, so it is never completed and nothing is refreshed.
        VerifyCompleted(Times.Never());
        _routing.Verify(r => r.Send(
            It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDictionary<string, string>?>()), Times.Never());
    }

    private OperationTask<SetComponentMetadataCommand> OpWith(
        Guid componentId, string? environment, Dictionary<string, string?> tags) =>
        new(new SetComponentMetadataCommand
            {
                ComponentId = componentId,
                Environment = environment,
                Tags = tags,
            },
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

    private async Task AssertRejected(OperationTask<SetComponentMetadataCommand> op)
    {
        var registry = new StubRegistry(found: true)
        {
            Registration = new ComponentRegistration
            {
                ComponentId = op.Command.ComponentId,
                ComponentType = ComponentType.VMHostAgent,
                MachineName = "host1",
                InboundQueue = "q",
            },
        };
        var handler = new SetComponentMetadataCommandHandler(_bus.Object, registry, _siteRepository.Object, _messaging.Object);

        await handler.Handle(op);

        registry.LastComponentId.Should().Be(Guid.Empty);
        VerifyCompleted(Times.Never());
    }

    // A tag key containing '=' must be rejected via the explicit raw-key check: reconstructing
    // "tag:{key}={value}" and canonicalizing splits on the first '=', so without the raw-key check
    // "k=v" would be silently reinterpreted as key "k" / value "v=..." instead of being rejected.
    [Fact]
    public async Task A_tag_key_containing_an_equals_sign_is_rejected()
    {
        await AssertRejected(OpWith(
            Guid.NewGuid(), null, new Dictionary<string, string?> { ["k=v"] = "r1" }));
    }

    [Fact]
    public async Task A_tag_key_containing_a_colon_is_rejected()
    {
        await AssertRejected(OpWith(
            Guid.NewGuid(), null, new Dictionary<string, string?> { ["k:v"] = "r1" }));
    }

    [Fact]
    public async Task A_tag_key_containing_whitespace_is_rejected()
    {
        await AssertRejected(OpWith(
            Guid.NewGuid(), null, new Dictionary<string, string?> { ["k v"] = "r1" }));
    }

    [Fact]
    public async Task An_environment_exceeding_the_max_length_is_rejected()
    {
        await AssertRejected(OpWith(
            Guid.NewGuid(), new string('a', 300), new Dictionary<string, string?>()));
    }

    [Fact]
    public async Task A_tag_whose_canonical_selector_exceeds_the_max_length_is_rejected()
    {
        await AssertRejected(OpWith(
            Guid.NewGuid(), null, new Dictionary<string, string?> { ["k"] = new string('b', 300) }));
    }

    private sealed class StubRegistry(bool found) : IComponentRegistryService
    {
        public Guid LastComponentId { get; private set; }
        public Guid? LastSiteId { get; private set; }
        public string? LastEnvironment { get; private set; }
        public IReadOnlyDictionary<string, string?> LastTags { get; private set; } = new Dictionary<string, string?>();
        public ComponentRegistration? Registration { get; init; }

        public Task<bool> SetMetadataAsync(
            Guid componentId, string? environment, Guid? siteId, IReadOnlyDictionary<string, string?>? tags,
            CancellationToken cancellationToken)
        {
            LastComponentId = componentId;
            LastEnvironment = environment;
            LastSiteId = siteId;
            LastTags = tags ?? new Dictionary<string, string?>();
            return Task.FromResult(found);
        }

        public Task<ComponentRegistration?> GetAsync(Guid componentId, CancellationToken cancellationToken)
            => Task.FromResult(Registration);

        public Task<ComponentRegistration> UpsertAsync(RegisterComponentCommand command, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ComponentRegistration?> RecordHeartbeatAsync(
            Guid componentId, Guid instanceId, IReadOnlyList<AppliedConfigVersion> appliedConfigVersions,
            CancellationToken ct) => throw new NotSupportedException();

        public Task RecordAppliedAsync(Guid componentId, ConfigDomain domain, string scope, long version, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<bool> DeregisterAsync(Guid componentId, Guid instanceId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<bool> RemoveRegistrationAsync(Guid componentId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ComponentRegistration>> GetActiveAsync(CancellationToken ct)
            => throw new NotSupportedException();
    }
}
