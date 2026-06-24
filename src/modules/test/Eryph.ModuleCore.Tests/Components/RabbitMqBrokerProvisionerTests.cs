using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Components;
using FluentAssertions;
using Xunit;

namespace Eryph.ModuleCore.Tests.Components;

public class RabbitMqBrokerProvisionerTests
{
    private static readonly Guid ComponentId = Guid.Parse("11112222-3333-4444-5555-666677778888");

    // The broker user name is the component-id URN; in a URL it must be percent-encoded (the ':' becomes
    // %3A), under both /api/users and /api/permissions.
    private const string EncodedUser = "urn%3Aeryph%3Acomponent%3A11112222-3333-4444-5555-666677778888";

    [Fact]
    public void UserName_is_the_component_id_urn()
    {
        ComponentBrokerIdentity.UserName(ComponentId)
            .Should().Be("urn:eryph:component:11112222-3333-4444-5555-666677778888");
    }

    [Fact]
    public async Task EnsureComponentAsync_creates_a_passwordless_user_and_grants_permissions()
    {
        var handler = new RecordingHandler();
        var provisioner = new RabbitMqBrokerProvisioner(Client(handler), new RabbitMqBrokerManagementOptions
        {
            VirtualHost = "eryph",
        });

        await provisioner.EnsureComponentAsync(ComponentId);

        handler.Requests.Should().HaveCount(2);

        var user = handler.Requests[0];
        user.Method.Should().Be(HttpMethod.Put);
        user.Uri.Should().Be($"http://broker:15672/api/users/{EncodedUser}");
        // Empty password_hash means the user can never authenticate with a password — only EXTERNAL.
        user.Body.Should().Contain("\"password_hash\":\"\"").And.Contain("\"tags\":\"\"");

        var permissions = handler.Requests[1];
        permissions.Method.Should().Be(HttpMethod.Put);
        permissions.Uri.Should().Be($"http://broker:15672/api/permissions/eryph/{EncodedUser}");
        permissions.Body.Should().Contain("\"configure\":\".*\"")
            .And.Contain("\"write\":\".*\"").And.Contain("\"read\":\".*\"");
    }

    [Fact]
    public async Task EnsureComponentAsync_throws_when_the_management_api_rejects_the_request()
    {
        var handler = new RecordingHandler { ResponseStatus = HttpStatusCode.Unauthorized };
        var provisioner = new RabbitMqBrokerProvisioner(Client(handler), new RabbitMqBrokerManagementOptions());

        var act = () => provisioner.EnsureComponentAsync(ComponentId);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task RemoveComponentAsync_deletes_the_user()
    {
        var handler = new RecordingHandler();
        var provisioner = new RabbitMqBrokerProvisioner(Client(handler), new RabbitMqBrokerManagementOptions());

        await provisioner.RemoveComponentAsync(ComponentId);

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Delete);
        handler.Requests[0].Uri.Should().Be($"http://broker:15672/api/users/{EncodedUser}");
    }

    [Fact]
    public async Task RemoveComponentAsync_treats_a_missing_user_as_success()
    {
        // Revocation is idempotent: deleting an already-absent user (404) must not throw — the desired
        // end state (no user) already holds.
        var handler = new RecordingHandler { ResponseStatus = HttpStatusCode.NotFound };
        var provisioner = new RabbitMqBrokerProvisioner(Client(handler), new RabbitMqBrokerManagementOptions());

        var act = () => provisioner.RemoveComponentAsync(ComponentId);

        await act.Should().NotThrowAsync();
    }

    private static HttpClient Client(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://broker:15672/") };

    private sealed record SentRequest(HttpMethod Method, string Uri, string Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];
        public HttpStatusCode ResponseStatus { get; init; } = HttpStatusCode.NoContent;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new SentRequest(request.Method, request.RequestUri!.ToString(), body));
            return new HttpResponseMessage(ResponseStatus);
        }
    }
}
