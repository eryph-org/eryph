using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Provisions and deprovisions a component's identity on the message broker so it can authenticate
/// with SASL EXTERNAL using its enrolled certificate. Enrollment ensures the user exists (a component
/// cannot join the bus otherwise); decommissioning removes it, which is the primary revocation
/// mechanism — an immediate hard cutoff independent of certificate expiry.
/// </summary>
/// <remarks>
/// Resolved as a collection: it is empty for deployments with no managed broker (eryph-zero's
/// in-memory bus), and a host that runs the split-runtime RabbitMQ broker appends an implementation.
/// So the enrollment path never reads a flag to decide whether to manage broker users — it just
/// provisions through whatever is registered.
/// </remarks>
public interface IComponentBrokerProvisioner
{
    /// <summary>Ensures a broker user exists for the component (idempotent). Called at enrollment and
    /// renewal, before the component connects to the bus.</summary>
    Task EnsureComponentAsync(Guid componentId, CancellationToken cancellationToken = default);

    /// <summary>Removes the component's broker user (idempotent). This revokes bus access immediately,
    /// regardless of whether the component's certificate is still valid.</summary>
    Task RemoveComponentAsync(Guid componentId, CancellationToken cancellationToken = default);
}
