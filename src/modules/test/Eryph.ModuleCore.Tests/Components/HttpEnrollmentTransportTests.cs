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
    // Mirrors the identity API's AddEryphApiSettings (snake_case + enums as strings): the server
    // deserializes the request and serializes the response with these conventions.
    private static readonly JsonSerializerOptions ServerJsonOptions = CreateServerJsonOptions();

    private static JsonSerializerOptions CreateServerJsonOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }

    [Fact]
    public async Task EnrollAsync_posts_the_authority_root_with_the_eryph_json_contract_and_deserializes_the_result()
    {
        var componentId = Guid.NewGuid();
        string? sentBody = null;
        var handler = new StubHandler((request, _) =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri.Should().Be(new Uri("https://identity.eryph.local:8080/components/enroll"));
            sentBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            // The server (AddEryphApiSettings) parses the request with snake_case + string enums; the
            // request must round-trip through those exact options or multi-word fields drop to null.
            var parsed = JsonSerializer.Deserialize<ComponentEnrollmentRequest>(sentBody, ServerJsonOptions)!;
            parsed.ComponentType.Should().Be(ComponentType.Controller);
            parsed.Fqdn.Should().Be("host.example");
            parsed.PublicKey.Should().Equal(1, 2, 3);
            parsed.ServerPublicKey.Should().Equal(4, 5, 6);
            parsed.Token.Should().Be("enroll-token");

            var result = new ComponentEnrollmentResult { ComponentId = componentId, Certificate = [9, 8, 7] };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(result, ServerJsonOptions), Encoding.UTF8, "application/json"),
            };
        });

        var transport = new HttpEnrollmentTransport(
            new HttpClient(handler),
            new EndpointResolver(new() { ["identity"] = "https://identity.eryph.local:8080/identity" }));

        var result = await transport.EnrollAsync(
            new ComponentEnrollmentRequest
            {
                ComponentType = ComponentType.Controller,
                Fqdn = "host.example",
                PublicKey = [1, 2, 3],
                ServerPublicKey = [4, 5, 6],
                Token = "enroll-token",
            },
            CancellationToken.None);

        // The wire format is the eryph contract: snake_case keys, enum as a string.
        sentBody.Should().Contain("\"component_type\":\"Controller\"")
            .And.Contain("\"public_key\":").And.Contain("\"server_public_key\":");
        result.ComponentId.Should().Be(componentId);
        result.Certificate.Should().Equal(9, 8, 7);
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
