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
    ComponentEnrollmentResult Enroll(ComponentEnrollmentRequest request);
}
