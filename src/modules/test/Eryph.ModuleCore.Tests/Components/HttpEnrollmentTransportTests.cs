using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Components;
using FluentAssertions;
using Xunit;

namespace Eryph.ModuleCore.Tests.Components;

public class HttpEnrollmentTransportTests
{
    [Fact]
    public async Task EnrollAsync_posts_to_the_authority_root_and_deserializes_the_result()
    {
        var componentId = Guid.NewGuid();
        var handler = new StubHandler((request, _) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri.Should().Be(new Uri("https://identity.eryph.local:8080/components/enroll"));

            var result = new ComponentEnrollmentResult { ComponentId = componentId, Certificate = [9] };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(result), Encoding.UTF8, "application/json"),
            };
        });

        var transport = new HttpEnrollmentTransport(
            new HttpClient(handler),
            new EndpointResolver(new() { ["identity"] = "https://identity.eryph.local:8080/identity" }));

        var result = await transport.EnrollAsync(
            new ComponentEnrollmentRequest { ComponentType = ComponentType.Identity, Fqdn = "x", PublicKey = [1] },
            CancellationToken.None);

        result.ComponentId.Should().Be(componentId);
    }

    [Fact]
    public async Task EnrollAsync_throws_on_a_non_success_response_so_the_client_retries()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var transport = new HttpEnrollmentTransport(
            new HttpClient(handler),
            new EndpointResolver(new() { ["identity"] = "https://identity.eryph.local:8080/identity" }));

        var act = () => transport.EnrollAsync(new ComponentEnrollmentRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request, cancellationToken));
    }
}
