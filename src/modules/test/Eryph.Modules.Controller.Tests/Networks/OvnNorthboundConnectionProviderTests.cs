using Dbosoft.OVN;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Controller.Components;
using Eryph.Modules.Controller.Networks;
using Eryph.StateDb.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimpleInjector;

namespace Eryph.Modules.Controller.Tests.Networks;

public class OvnNorthboundConnectionProviderTests
{
    [Theory]
    [InlineData("host.domain.example", "host.domain.example", true)]
    [InlineData("HOST.domain.example", "host.domain.example", true)] // DNS is case-insensitive
    [InlineData("host", "host.domain.example", false)] // short name never equals the FQDN
    [InlineData("other.domain.example", "host.domain.example", false)] // a genuinely different host
    public void IsColocated_ComparesFullyQualifiedNames(
        string componentMachineName, string localHostId, bool expected)
    {
        OvnNorthboundConnectionProvider.IsColocated(componentMachineName, localHostId)
            .Should().Be(expected);
    }

    [Fact]
    public async Task GetNorthboundConnection_NoNetworkComponent_UsesLocalPipe()
    {
        var pipe = new LocalOVSWithOVNSettings().NorthDBConnection;
        var provider = CreateProvider([], pipe);

        var result = await provider.GetNorthboundConnection().Run();

        result.IsSucc.Should().BeTrue();
        result.Match(c => c, e => throw new Exception(e.ToString()))
            .Should().BeSameAs(pipe);
    }

    [Fact]
    public async Task GetNorthboundConnection_ColocatedNetworkComponent_UsesLocalPipe()
    {
        var pipe = new LocalOVSWithOVNSettings().NorthDBConnection;
        // A network component whose host identity is this host: it is co-located, so the controller
        // reaches the databases over the local pipe even though it advertises no remote endpoint.
        var colocated = NetworkComponent(ComponentIdentity.GetLocalHostId(), null);
        var provider = CreateProvider([colocated], pipe);

        var result = await provider.GetNorthboundConnection().Run();

        result.IsSucc.Should().BeTrue();
        result.Match(c => c, e => throw new Exception(e.ToString()))
            .Should().BeSameAs(pipe);
    }

    [Fact]
    public async Task GetNorthboundConnection_RemoteComponentWithoutEndpoint_FailsFast()
    {
        var pipe = new LocalOVSWithOVNSettings().NorthDBConnection;
        var remote = NetworkComponent("remote-host.example", null);
        var provider = CreateProvider([remote], pipe);

        var result = await provider.GetNorthboundConnection().Run();

        result.IsFail.Should().BeTrue();
        result.Match(_ => "", e => e.Message)
            .Should().Contain("has not advertised");
    }

    [Fact]
    public async Task GetNorthboundConnection_PrefersMostRecentlyHeartbeatingNetworkComponent()
    {
        var pipe = new LocalOVSWithOVNSettings().NorthDBConnection;
        // A stale registration that still names this host (would be misdetected as co-located) and a
        // fresher remote one. The provider must follow the live remote component, not the stale local.
        var staleLocal = NetworkComponent(
            ComponentIdentity.GetLocalHostId(), null,
            DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10));
        var freshRemote = NetworkComponent(
            "remote-host.example", null, DateTimeOffset.UtcNow);
        var provider = CreateProvider([staleLocal, freshRemote], pipe);

        var result = await provider.GetNorthboundConnection().Run();

        // It picked the remote component (no endpoint advertised) and failed fast, rather than
        // returning the local pipe it would have for the stale co-located registration.
        result.IsFail.Should().BeTrue();
        result.Match(_ => "", e => e.Message)
            .Should().Contain("has not advertised");
    }

    private static OvnNorthboundConnectionProvider CreateProvider(
        IReadOnlyList<ComponentRegistration> active, OvsDbConnection localPipe)
    {
        var ovnSettings = new Mock<IOVNSettings>();
        ovnSettings.SetupGet(s => s.NorthDBConnection).Returns(localPipe);

        return new OvnNorthboundConnectionProvider(
            new Container(),
            new StubRegistry(active),
            ovnSettings.Object,
            Mock.Of<ISystemEnvironment>(),
            NullLogger<OvnNorthboundConnectionProvider>.Instance);
    }

    private static ComponentRegistration NetworkComponent(
        string machineName, string? advertisedEndpoint, DateTimeOffset? lastHeartbeat = null)
    {
        var advertised = new Dictionary<string, string>();
        if (advertisedEndpoint is not null)
            advertised[OvnRemoteEndpoints.NorthboundName] = advertisedEndpoint;

        return new ComponentRegistration
        {
            MachineName = machineName,
            InboundQueue = "q",
            ComponentType = ComponentType.Network,
            AdvertisedEndpoints = advertised,
            LastHeartbeat = lastHeartbeat ?? DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Hand-written stub: <see cref="IComponentRegistryService"/> is internal, which Moq cannot proxy
    /// without marking the production assembly InternalsVisibleTo the proxy generator. Only
    /// GetActiveAsync is exercised here.
    /// </summary>
    private sealed class StubRegistry(IReadOnlyList<ComponentRegistration> active) : IComponentRegistryService
    {
        public Task<bool> SetMetadataAsync(
            Guid componentId, string? environment, IReadOnlyDictionary<string, string?>? tags,
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
