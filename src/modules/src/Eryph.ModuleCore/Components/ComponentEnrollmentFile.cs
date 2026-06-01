using System;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// The self-contained bootstrap artifact an operator delivers to a component out-of-band (one file).
/// Produced by the identity service's enrollment command; imported by the component on first start.
/// It carries everything needed to bootstrap trust and enroll: the identity CA certificate (so the
/// component can trust the identity TLS endpoint without any pre-existing trust), the endpoint to
/// enroll against, and a one-time token authorizing exactly one enrollment for the named component
/// type. The component then enrolls both its own TLS server certificate and its bus client
/// certificate before it completes startup.
/// </summary>
/// <remarks>
/// Serialized as JSON with the eryph API conventions (snake_case, enums as strings) so it round-trips
/// through the same options the identity API uses. The token is signed by the identity service and
/// is validated there on enrollment — the file is not a secret store, it is a signed voucher plus the
/// trust anchor needed to use it.
/// </remarks>
public sealed class ComponentEnrollmentFile
{
    /// <summary>The component type this enrollment is bound to (the token authorizes only this type).</summary>
    public ComponentType ComponentType { get; init; }

    /// <summary>
    /// The host FQDN this enrollment is bound to. The token authorizes enrollment of
    /// <see cref="ComponentType"/> only for the component whose self-reported FQDN matches this value,
    /// so an enrollment file produces a certificate for exactly one host identity. Informational here
    /// (the binding is enforced from the signed token); surfaced so automated rollout can see which
    /// host a file is for.
    /// </summary>
    public string Fqdn { get; init; } = "";

    /// <summary>The identity service base URL the component enrolls against (HTTPS).</summary>
    public string IdentityEndpoint { get; init; } = "";

    /// <summary>The identity CA root certificate, DER-encoded — the trust anchor the component pins
    /// to validate the identity TLS endpoint (and the broker, once enrolled).</summary>
    public byte[] IdentityCaCertificate { get; init; } = [];

    /// <summary>The one-time, identity-signed enrollment token.</summary>
    public string Token { get; init; } = "";

    /// <summary>When the token expires (after which enrollment must be re-issued by the operator).</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}
