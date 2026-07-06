using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.Modules.Controller.Components;
using Eryph.Rebus;
using Eryph.StateDb.Model;
using Moq;
using Rebus.Bus;

namespace Eryph.Modules.Controller.Tests.Components;

/// <summary>
/// Verifies the config-management command handlers (the write/read side of the config-management API):
/// setting a domain stores a new authored version and triggers distribution; getting a domain replies
/// with the current version. <see cref="IAuthoredConfigStore"/> is internal and cannot be proxied by
/// Moq, so it is hand-stubbed.
/// </summary>
public class ConfigManagementCommandHandlerTests
{
    [Fact]
    public async Task Set_stores_a_new_version_and_triggers_a_refresh()
    {
        var store = new FakeStore();
        var bus = new Mock<IBus> { DefaultValue = DefaultValue.Mock };
        var handler = new SetConfigDomainCommandHandler(bus.Object, store);

        await handler.Handle(new SetConfigDomainCommand
        {
            Domain = ConfigDomain.PlacementConfig,
            Payload = """{"Datastores":["ds1"]}""",
            Author = "alice",
        });

        store.Added.Should().ContainSingle();
        store.Added[0].Should().Be(
            (ConfigDomain.PlacementConfig, ConfigScope.Default, """{"Datastores":["ds1"]}""", "alice"));

        Mock.Get(bus.Object.Advanced.Routing).Verify(r => r.Send(
                QueueNames.Controllers,
                It.Is<RefreshConfigDomainCommand>(c => c.Domain == ConfigDomain.PlacementConfig),
                It.IsAny<IDictionary<string, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Set_with_no_payload_is_rejected_and_nothing_is_stored_or_sent()
    {
        var store = new FakeStore();
        var bus = new Mock<IBus> { DefaultValue = DefaultValue.Mock };
        var handler = new SetConfigDomainCommandHandler(bus.Object, store);

        var act = () => handler.Handle(
            new SetConfigDomainCommand { Domain = ConfigDomain.PlacementConfig, Payload = null });

        await act.Should().ThrowAsync<InvalidOperationException>();
        store.Added.Should().BeEmpty();
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
