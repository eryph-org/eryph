using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Components;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Server-side component enrollment: validates a request against the configured
/// <see cref="IComponentEnrollmentPolicy"/>, then issues a component certificate from the
/// component CA and returns it together with the current CA trust bundle.
/// </summary>
public interface IComponentEnrollmentService
{
    /// <summary>
    /// Issues a certificate for the request after policy authorization. Throws
    /// <see cref="ComponentEnrollmentException"/> when the request is unauthorized or invalid.
    /// </summary>
    Task<ComponentEnrollmentResult> EnrollAsync(
        ComponentEnrollmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews a component's certificates: authenticated by the component's current (still-valid,
    /// CA-issued) certificate rather than a one-time token. The identity to renew is taken from
    /// <paramref name="clientCertificate"/>, so a component can only renew its own identity. Throws
    /// <see cref="ComponentEnrollmentException"/> when the certificate is not a trusted component
    /// certificate or the request is invalid.
    /// </summary>
    Task<ComponentEnrollmentResult> RenewAsync(
        X509Certificate2 clientCertificate,
        ComponentEnrollmentRequest request,
        CancellationToken cancellationToken = default);
}
