using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.Modules.Controller;
using Eryph.Modules.Controller.Components;
using Eryph.StateDb.Model;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.Tests;

public class RegistryBackedComponentRegistryTests
{
    [Fact]
    public void GetHostAgents_ReturnsVmHostAgentsOrderedByName_SkippingNonAgentAndMalformedRows()
    {
        var active = new[]
        {
            Registration(ComponentType.VMHostAgent, "eryph.vmhostagent.WASD30"),
            Registration(ComponentType.Network, "eryph.network.WASD10"),      // not an agent → filtered
            Registration(ComponentType.VMHostAgent, "eryph.vmhostagent.wasd05"),
            Registration(ComponentType.VMHostAgent, "eryph.vmhostagent."),    // malformed → skipped
            Registration(ComponentType.VMHostAgent, "eryph.vmhostagent.WASD20"),
        };

        var agents = CreateRegistry(active).GetHostAgents();

        // Deterministic, case-insensitive order so every consumer resolves to the same first agent.
        agents.Select(a => a.AgentName).ToList()
            .Should().Equal("wasd05", "WASD20", "WASD30");
    }

    [Fact]
    public void GetHostAgents_NoAgents_ReturnsEmpty()
    {
        CreateRegistry([]).GetHostAgents().Should().BeEmpty();
    }

    private static RegistryBackedComponentRegistry CreateRegistry(
        IReadOnlyList<ComponentRegistration> active)
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.Register<IComponentRegistryService>(() => new StubRegistry(active), Lifestyle.Scoped);
        return new RegistryBackedComponentRegistry(container);
    }

    [Fact]
    public void ToHostAgent_HostAgentRow_MapsAgentNameFromInboundQueue()
    {
        var registration = Registration(ComponentType.VMHostAgent, "eryph.vmhostagent.WASD12");

        var result = RegistryBackedComponentRegistry.ToHostAgent(registration);

        result.IsSome.Should().BeTrue();
        var agent = result.IfNoneUnsafe(() => null!);
        // The routing key is the queue suffix (Environment.MachineName), not the FQDN MachineName.
        agent.AgentName.Should().Be("WASD12");
        agent.ChassisName.Should().Be(EryphConstants.Networking.LocalChassisName);
        agent.ChassisPriority.Should().Be(1);
    }

    [Theory]
    [InlineData("eryph.genepool.WASD12")] // a different component's queue shape
    [InlineData("eryph.vmhostagent")] // no suffix
    [InlineData("eryph.vmhostagent.")] // empty suffix
    [InlineData("something.else.WASD12")] // wrong prefix
    public void ToHostAgent_MalformedInboundQueue_ReturnsNone(string inboundQueue)
    {
        var registration = Registration(ComponentType.VMHostAgent, inboundQueue);

        RegistryBackedComponentRegistry.ToHostAgent(registration).IsNone.Should().BeTrue();
    }

    private static ComponentRegistration Registration(ComponentType type, string inboundQueue) =>
        new()
        {
            ComponentType = type,
            MachineName = "wasd12.was.corp",
            InboundQueue = inboundQueue,
        };

    /// <summary>
    /// Hand-written stub: <see cref="IComponentRegistryService"/> is internal, which Moq cannot proxy.
    /// Only GetActiveAsync is exercised here.
    /// </summary>
    private sealed class StubRegistry(IReadOnlyList<ComponentRegistration> active) : IComponentRegistryService
    {
        public Task<bool> SetMetadataAsync(
            Guid componentId, string? environment, IReadOnlyDictionary<string, string>? tags,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ComponentRegistration> UpsertAsync(RegisterComponentCommand command,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ComponentRegistration?> RecordHeartbeatAsync(Guid componentId, Guid instanceId,
            IReadOnlyList<AppliedConfigVersion> appliedConfigVersions, CancellationToken cancellationToken)
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
            => Task.FromResult(active);
    }
}
