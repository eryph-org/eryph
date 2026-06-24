using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Sends enrollment requests to the identity service over HTTPS. The endpoint is part of the
/// versioned identity API (<c>/v1/components/enroll</c>); the component trusts the identity server
/// certificate via the pre-provisioned CA root. A non-success response throws so the enrollment
/// client retries.
/// </summary>
public sealed class HttpEnrollmentTransport(HttpClient httpClient, IEndpointResolver endpointResolver)
    : IEnrollmentTransport
{
    // Must match the identity API's JSON conventions (ApiProvider's AddEryphApiSettings): snake_case
    // property names and enums as strings. Otherwise multi-word fields (component_type, public_key)
    // do not bind on the server and the request is rejected.
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<ComponentEnrollmentResult> EnrollAsync(
        ComponentEnrollmentRequest request,
        CancellationToken cancellationToken) =>
        PostAsync("enroll", request, cancellationToken);

    public Task<ComponentEnrollmentResult> RenewAsync(
        ComponentEnrollmentRequest request,
        CancellationToken cancellationToken) =>
        // Renewal authenticates with the component's current client certificate, which the underlying
        // HttpClient's handler presents at the TLS layer; the request body (new public keys) is the same
        // shape as enrollment, only the endpoint and the credential differ.
        PostAsync("renew", request, cancellationToken);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private async Task<ComponentEnrollmentResult> PostAsync(
        string action, ComponentEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var identity = endpointResolver.GetEndpoint("identity");
        // Preserve the identity endpoint's path base (e.g. "/identity"): the whole identity API,
        // including these versioned endpoints, is hosted under it. Appending to the authority alone would
        // drop the base and 404. GetLeftPart(Path) strips any query/fragment; TrimEnd avoids a double
        // slash so a base with or without a trailing slash both compose correctly.
        var identityBase = identity.GetLeftPart(UriPartial.Path).TrimEnd('/');
        var requestUri = new Uri(identityBase + "/v1/components/" + action);

        var payload = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<ComponentEnrollmentResult>(body, JsonOptions)
               ?? throw new InvalidOperationException("The enrollment response was empty.");
    }
}
