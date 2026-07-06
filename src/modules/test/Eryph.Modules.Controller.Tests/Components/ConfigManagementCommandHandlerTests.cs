using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.Rebus;
using Eryph.StateDb.Model;
using Moq;
using Rebus.Bus;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the config-management command handlers (the write/read side of the config-management API):
/// setting an authorable domain with a valid payload stores a new version, triggers distribution and
/// replies success; a wrong-domain or malformed write is rejected (nothing stored or sent); getting a
/// domain replies with the current version. <see cref="IAuthoredConfigStore"/> is internal and cannot
/// be proxied by Moq, so it is hand-stubbed.
/// </summary>
public class ConfigManagementCommandHandlerTests
{
    private const string ValidPlacement = """{"Datastores":["ds1"],"Environments":["e1"]}""";

    [Fact]
    public async Task Set_stores_a_new_version_triggers_a_refresh_and_replies_success()
    {
        var store = new FakeStore();
        var bus = new Mock<IBus> { DefaultValue = DefaultValue.Mock };
        var handler = new SetConfigDomainCommandHandler(bus.Object, store);

        await handler.Handle(new SetConfigDomainCommand
        {
            Domain = ConfigDomain.PlacementConfig,
            Payload = ValidPlacement,
            Author = "alice",
        });

        store.Added.Should().ContainSingle();
        store.Added[0].Should().Be((ConfigDomain.PlacementConfig, ConfigScope.Default, ValidPlacement, "alice"));

        Mock.Get(bus.Object.Advanced.Routing).Verify(r => r.Send(
                QueueNames.Controllers,
                It.Is<RefreshConfigDomainCommand>(c => c.Domain == ConfigDomain.PlacementConfig),
                It.IsAny<IDictionary<string, string>>()),
            Times.Once);

        bus.Verify(b => b.Reply(
                It.Is<SetConfigDomainResponse>(r => r.Success && r.Version == 1 && r.Error == null),
                It.IsAny<IDictionary<string, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Set_rejects_a_system_derived_domain_without_storing_or_distributing()
    {
        var store = new FakeStore();
        var bus = new Mock<IBus> { DefaultValue = DefaultValue.Mock };
        var handler = new SetConfigDomainCommandHandler(bus.Object, store);

        // OvnCluster is system-derived (no authored source honors it).
        await handler.Handle(new SetConfigDomainCommand { Domain = ConfigDomain.OvnCluster, Payload = "{}" });

        store.Added.Should().BeEmpty();
        bus.Verify(b => b.Reply(
                It.Is<SetConfigDomainResponse>(r => !r.Success && r.Error != null),
                It.IsAny<IDictionary<string, string>>()),
            Times.Once);
        Mock.Get(bus.Object.Advanced.Routing).Verify(r => r.Send(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("""{"datastores":["ds1"]}""")] // wrong-cased member — would deserialize to an empty vocabulary
    public async Task Set_rejects_an_invalid_payload_without_storing_or_distributing(string? payload)
    {
        var store = new FakeStore();
        var bus = new Mock<IBus> { DefaultValue = DefaultValue.Mock };
        var handler = new SetConfigDomainCommandHandler(bus.Object, store);

        await handler.Handle(new SetConfigDomainCommand { Domain = ConfigDomain.PlacementConfig, Payload = payload });

        store.Added.Should().BeEmpty();
        bus.Verify(b => b.Reply(
                It.Is<SetConfigDomainResponse>(r => !r.Success && r.Error != null),
                It.IsAny<IDictionary<string, string>>()),
            Times.Once);
        Mock.Get(bus.Object.Advanced.Routing).Verify(r => r.Send(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task Get_replies_with_the_current_authored_version()
    {
        var store = new FakeStore
        {
            Current = new AuthoredConfig
            {
                Id = Guid.NewGuid(), Domain = ConfigDomain.PlacementConfig,
                Scope = ConfigScope.Default, Version = 7, Payload = "p7",
            },
        };
        var bus = new Mock<IBus> { DefaultValue = DefaultValue.Mock };
        var handler = new GetConfigDomainCommandHandler(bus.Object, store);

        await handler.Handle(new GetConfigDomainCommand { Domain = ConfigDomain.PlacementConfig });

        bus.Verify(b => b.Reply(
                It.Is<ConfigDomainResponse>(r =>
                    r.Domain == ConfigDomain.PlacementConfig && r.Version == 7 && r.Payload == "p7"),
                It.IsAny<IDictionary<string, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Get_replies_empty_when_nothing_is_authored()
    {
        var store = new FakeStore { Current = null };
        var bus = new Mock<IBus> { DefaultValue = DefaultValue.Mock };
        var handler = new GetConfigDomainCommandHandler(bus.Object, store);

        await handler.Handle(new GetConfigDomainCommand { Domain = ConfigDomain.Endpoints });

        bus.Verify(b => b.Reply(
                It.Is<ConfigDomainResponse>(r =>
                    r.Domain == ConfigDomain.Endpoints && r.Version == null && r.Payload == null),
                It.IsAny<IDictionary<string, string>>()),
            Times.Once);
    }

    private sealed class FakeStore : IAuthoredConfigStore
    {
        public List<(ConfigDomain, string, string, string?)> Added { get; } = [];

        public AuthoredConfig? Current { get; init; }

        public Task<AuthoredConfig> AddVersionAsync(
            ConfigDomain domain, string scope, string payload, string? author, CancellationToken cancellationToken)
        {
            Added.Add((domain, scope, payload, author));
            return Task.FromResult(new AuthoredConfig
            {
                Id = Guid.NewGuid(), Domain = domain, Scope = scope, Version = Added.Count, Payload = payload,
            });
        }

        public Task<AuthoredConfig?> GetCurrentAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => Task.FromResult(Current);

        public Task<IReadOnlyList<AuthoredConfig>> GetHistoryAsync(
            ConfigDomain domain, string scope, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
