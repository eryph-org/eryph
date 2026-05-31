using System.Threading;
using System.Threading.Tasks;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Sends an enrollment request to the identity service's enrollment endpoint (over HTTPS, off the
/// bus) and returns the issued certificate result. Abstracts the transport so the enrollment
/// client's retry/persistence logic can be tested without a live HTTP endpoint.
/// </summary>
public interface IEnrollmentTransport
{
    Task<ComponentEnrollmentResult> EnrollAsync(
        ComponentEnrollmentRequest request,
        CancellationToken cancellationToken);
}
