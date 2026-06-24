using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.Core;
using Eryph.Modules.AspNetCore.TestBase;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.TestBase;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.Modules.ComputeApi.Tests.Integration.Endpoints.Catlets;

public class OpenSshChannelConnectTests : InMemoryStateDbTestBase
{
    private const string RemoteAccessScope = "compute:catlets:remote-access";
    private const string AgentName = "test-agent";

    private static readonly Guid CatletId = Guid.NewGuid();
    private static readonly Guid CatletMetadataId = Guid.NewGuid();

    private readonly WebModuleFactory<ComputeApiModule> _factory;
    private readonly CapturingAgentChannelForwarder _forwarder = new();

    public OpenSshChannelConnectTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
        _factory = new WebModuleFactory<ComputeApiModule>()
            .WithApiHost(ConfigureDatabase, RegisterStateStore, _forwarder);
    }

    [Fact]
    public async Task Connect_NonWebSocketRequest_ReturnsBadRequest()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true)
            .GetAsync($"v1/catlets/{CatletId}/guest-services/ssh-channel/connect?token=the-token");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _forwarder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Connect_WithoutRemoteAccessScope_IsForbidden()
    {
        var response = await _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, "compute:read", true)
            .GetAsync($"v1/catlets/{CatletId}/guest-services/ssh-channel/connect?token=the-token");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _forwarder.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Connect_WebSocketWithToken_ForwardsToTheCatletsAgent()
    {
        using var authClient = _factory.CreateDefaultClient()
            .SetEryphToken(EryphConstants.DefaultTenantId, EryphConstants.SystemClientId, RemoteAccessScope, true);
        var authorization = authClient.DefaultRequestHeaders.Authorization!;

        var wsClient = _factory.Server.CreateWebSocketClient();
        wsClient.ConfigureRequest = request => request.Headers["Authorization"] = authorization.ToString();

        var uri = new Uri(_factory.Server.BaseAddress,
            $"v1/catlets/{CatletId}/guest-services/ssh-channel/connect?token=the-token");
        using var webSocket = await wsClient.ConnectAsync(uri, CancellationToken.None);

        // The forwarder closes the socket once it has been handed the connection; receiving that close
        // guarantees ForwardAsync ran before we assert on what it captured.
        var buffer = new byte[8];
        await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        _forwarder.CallCount.Should().Be(1);
        _forwarder.AgentName.Should().Be(AgentName);
        _forwarder.Token.Should().Be("the-token");
    }

    protected override async Task SeedAsync(IStateStore stateStore)
    {
        await SeedDefaultTenantAndProject();

        var metadata = await stateStore.For<CatletMetadata>().AddAsync(new CatletMetadata
        {
            Id = CatletMetadataId,
            Metadata = new CatletMetadataContent(),
        });

        await stateStore.For<Catlet>().AddAsync(new Catlet
        {
            Id = CatletId,
            ProjectId = EryphConstants.DefaultProjectId,
            MetadataId = metadata.Id,
            Name = "test-catlet",
            AgentName = AgentName,
            DataStore = EryphConstants.DefaultDataStoreName,
            Environment = EryphConstants.DefaultEnvironmentName,
        });
    }

    public override async Task DisposeAsync()
    {
        _factory.Dispose();
        await base.DisposeAsync();
    }
}
