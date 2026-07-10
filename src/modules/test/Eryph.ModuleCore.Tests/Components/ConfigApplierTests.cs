using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Rebus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rebus.Bus;
using Rebus.Bus.Advanced;

namespace Eryph.ModuleCore.Tests.Components;

/// <summary>
/// Verifies <see cref="ConfigApplier"/>: a bundle is skipped only when it is not newer than the already
/// applied version FOR THE SAME SCOPE (a different scope always applies, even at a lower version), and a
/// successful apply is always acknowledged with a <see cref="ConfigAppliedEvent"/> that echoes the
/// bundle's scope.
/// </summary>
public class ConfigApplierTests
{
    private readonly Mock<IBus> _bus = new();
    private readonly Mock<IRoutingApi> _routing = new();
    private readonly ComponentIdentity _identity = new(ComponentType.VMHostAgent, "queue");
    private readonly ComponentConfigState _state = new();
    private readonly StubRealizer _realizer = new(ConfigDomain.StorageConfig);

    public ConfigApplierTests()
    {
        var advanced = new Mock<IAdvancedApi>();
        advanced.Setup(a => a.Routing).Returns(_routing.Object);
        _bus.Setup(b => b.Advanced).Returns(advanced.Object);
    }

    private ConfigApplier CreateApplier() =>
        new(_bus.Object, _identity, _state, [_realizer], NullLogger<ConfigApplier>.Instance);

    [Fact]
    public async Task Bundle_for_a_different_scope_is_applied_even_when_the_default_scope_has_a_higher_version()
    {
        _state.SetApplied(ConfigDomain.StorageConfig, "", 5);
        var applier = CreateApplier();

        await applier.ApplyAsync(new ConfigBundle
        {
            Domain = ConfigDomain.StorageConfig,
            Scope = "env:edge",
            Version = 1,
            Payload = "payload",
        });

        _realizer.Invocations.Should().ContainSingle();
        _realizer.Invocations[0].Version.Should().Be(1);
        _realizer.Invocations[0].Payload.Should().Be("payload");
        _state.GetAppliedVersion(ConfigDomain.StorageConfig, "env:edge").Should().Be(1);
    }

    [Fact]
    public async Task Bundle_not_newer_than_the_applied_version_for_the_same_scope_is_skipped()
    {
        _state.SetApplied(ConfigDomain.StorageConfig, "", 5);
        var applier = CreateApplier();

        await applier.ApplyAsync(new ConfigBundle
        {
            Domain = ConfigDomain.StorageConfig,
            Scope = "",
            Version = 5,
            Payload = "payload",
        });

        _realizer.Invocations.Should().BeEmpty();
        _routing.Verify(r => r.Send(
            It.IsAny<string>(), It.IsAny<object>(), It.IsAny<System.Collections.Generic.IDictionary<string, string>?>()),
            Times.Never());
    }

    [Fact]
    public async Task Successful_apply_sends_a_ConfigAppliedEvent_echoing_the_bundles_scope()
    {
        var applier = CreateApplier();

        await applier.ApplyAsync(new ConfigBundle
        {
            Domain = ConfigDomain.StorageConfig,
            Scope = "env:edge",
            Version = 1,
            Payload = "payload",
        });

        _routing.Verify(r => r.Send(
            QueueNames.Controllers,
            It.Is<ConfigAppliedEvent>(e => e.Scope == "env:edge" && e.Success && e.Version == 1),
            It.IsAny<System.Collections.Generic.IDictionary<string, string>?>()),
            Times.Once());
    }

    private sealed class StubRealizer(ConfigDomain domain) : IConfigRealizer
    {
        public ConfigDomain Domain { get; } = domain;

        public List<(long Version, string Payload)> Invocations { get; } = [];

        public Task ApplyAsync(long version, string payload, CancellationToken cancellationToken)
        {
            Invocations.Add((version, payload));
            return Task.CompletedTask;
        }
    }
}
