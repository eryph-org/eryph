using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Sends enrollment requests to the identity service over HTTPS. The endpoint lives at the
/// authority root (<c>/components/enroll</c>); the component trusts the identity server certificate
/// via the pre-provisioned CA root. A non-success response throws so the enrollment client retries.
/// </summary>
public sealed class HttpEnrollmentTransport(HttpClient httpClient, IEndpointResolver endpointResolver)
    : IEnrollmentTransport
{
    public async Task<ComponentEnrollmentResult> EnrollAsync(
        ComponentEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var identity = endpointResolver.GetEndpoint("identity");
        var enrollUri = new Uri(new Uri(identity.GetLeftPart(UriPartial.Authority)), "/components/enroll");

        var payload = JsonSerializer.Serialize(request);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(enrollUri, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<ComponentEnrollmentResult>(body)
            ?? throw new InvalidOperationException("The enrollment response was empty.");
    }
}
