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
    /// <summary>
    /// Enrolls a not-yet-enrolled component: authenticates with the one-time token in the request and
    /// returns the issued certificate material.
    /// </summary>
    Task<ComponentEnrollmentResult> EnrollAsync(
        ComponentEnrollmentRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Renews an already-enrolled component: authenticates with the component's CURRENT mTLS client
    /// certificate (presented at the TLS layer, not via the request) rather than the one-time token,
    /// which cannot be reused. The token field of the request is ignored by the renew endpoint.
    /// </summary>
    Task<ComponentEnrollmentResult> RenewAsync(
        ComponentEnrollmentRequest request,
        CancellationToken cancellationToken);
}
