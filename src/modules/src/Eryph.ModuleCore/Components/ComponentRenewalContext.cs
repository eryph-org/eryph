using System.Threading;
using Microsoft.Extensions.Logging;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Everything the periodic renewal service needs to renew the component's certificate, captured by
/// the host's mTLS bootstrap (<see cref="ComponentMtlsTransport"/>) and registered for the
/// <see cref="ComponentEnrollmentHostedService"/> to resolve. Only the split-runtime hosts register
/// it; eryph-zero (in-memory bus) does not, which is what keeps the renewal service off there.
/// </summary>
public sealed class ComponentRenewalContext
{
    public required IComponentCertificateStore Store { get; init; }
    public required ComponentIdentity Identity { get; init; }
    public required IEndpointResolver EndpointResolver { get; init; }
    public required ComponentEnrollmentClientOptions Options { get; init; }

    /// <summary>Path to the pinned identity CA bundle used to validate the identity TLS endpoint.</summary>
    public required string TrustAnchorBundlePath { get; init; }

    /// <summary>
    /// Carried from the host (which always has it) rather than resolved from the module container, so
    /// the renewal service does not depend on each module registering <see cref="ILoggerFactory"/>.
    /// </summary>
    public required ILoggerFactory LoggerFactory { get; init; }

    /// <summary>
    /// Serializes renewal so the periodic check and an operator-forced renewal cannot run concurrently
    /// (a double <c>/renew</c> wastes an enrollment and races the certificate-file write). Both callers
    /// acquire it around the renewal. One per context instance (one per component process).
    /// </summary>
    public SemaphoreSlim RenewalLock { get; } = new(1, 1);
}
