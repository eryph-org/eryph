using Dbosoft.Rebus.Operations;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.Rebus;
using Eryph.StateDb.Model;
using Moq;
using Rebus.Bus;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the config-management operation handler: a valid authorable write stores a canonical
/// version, re-distributes it and completes the operation; a wrong-domain or malformed write neither
/// stores nor distributes and does not complete (it fails the operation instead).
/// <see cref="IAuthoredConfigStore"/> is internal and cannot be proxied by Moq, so it is hand-stubbed.
/// </summary>
public class SetConfigDomainCommandHandlerTests
{
    private const string ValidStorage = "datastores: [{name: ds1}]";

    private readonly Mock<ITaskMessaging> _messaging = new();
    private readonly Mock<IBus> _bus = new() { DefaultValue = DefaultValue.Mock };
    private readonly FakeStore _store = new();

    private SetConfigDomainCommandHandler CreateHandler() => new(_bus.Object, _store, _messaging.Object);

    private static OperationTask<SetConfigDomainCommand> Op(ConfigDomain domain, string? payload) =>
        new(new SetConfigDomainCommand { Domain = domain, Payload = payload, Author = "alice" },
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

    private void VerifyCompleted(Times times) =>
        _messaging.Verify(m => m.CompleteTask(
            It.IsAny<IOperationTaskMessage>(), It.IsAny<IDictionary<string, string>?>()), times);

    private void VerifyDistributed(Times times) =>
        Mock.Get(_bus.Object.Advanced.Routing).Verify(r => r.Send(
            It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()), times);

    [Fact]
    public async Task Valid_set_stores_a_version_distributes_and_completes()
    {
        await CreateHandler().Handle(Op(ConfigDomain.StorageConfig, ValidStorage));

        _store.Added.Should().ContainSingle();
        _store.Added[0].domain.Should().Be(ConfigDomain.StorageConfig);
        _store.Added[0].author.Should().Be("alice");

        Mock.Get(_bus.Object.Advanced.Routing).Verify(r => r.Send(
                QueueNames.Controllers,
                It.Is<RefreshConfigDomainCommand>(c => c.Domain == ConfigDomain.StorageConfig),
                It.IsAny<IDictionary<string, string>>()),
            Times.Once);
        VerifyCompleted(Times.Once());
    }

    [Fact]
    public async Task System_derived_domain_is_not_stored_distributed_or_completed()
    {
        await CreateHandler().Handle(Op(ConfigDomain.OvnCluster, "{}"));

        _store.Added.Should().BeEmpty();
        VerifyDistributed(Times.Never());
        VerifyCompleted(Times.Never());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("""{"Datastores":["ds1"]}""")] // wrong-cased key ('datastores' is the underscored name)
    public async Task Invalid_payload_is_not_stored_distributed_or_completed(string? payload)
    {
        await CreateHandler().Handle(Op(ConfigDomain.StorageConfig, payload));

        _store.Added.Should().BeEmpty();
        VerifyDistributed(Times.Never());
        VerifyCompleted(Times.Never());
    }

    private sealed class FakeStore : IAuthoredConfigStore
    {
        public List<(ConfigDomain domain, string scope, string payload, string? author)> Added { get; } = [];

        public Task<AuthoredConfig> AddVersionAsync(
            ConfigDomain domain, string scope, string payload, string? author, CancellationToken cancellationToken)
        {
            Added.Add((domain, scope, payload, author));
            return System.Threading.Tasks.Task.FromResult(new AuthoredConfig
            {
                Id = Guid.NewGuid(), Domain = domain, Scope = scope, Version = Added.Count, Payload = payload,
            });
        }

        public Task<AuthoredConfig?> GetCurrentAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<AuthoredConfig>> GetHistoryAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
