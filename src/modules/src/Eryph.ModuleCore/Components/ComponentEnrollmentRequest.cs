using System.Collections.Generic;
using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// A component's request to enroll and obtain its certificates. Sent over HTTPS to the identity
/// service (off the bus — the bus is what the resulting certificates secure). The component
/// generates its key pairs locally; only the public keys are sent, so the private keys never leave
/// the component. Authorized by a one-time enrollment token (delivered out-of-band in the
/// enrollment file, see <see cref="ComponentEnrollmentFile"/>).
/// </summary>
public sealed class ComponentEnrollmentRequest
{
    public ComponentType ComponentType { get; init; }

    /// <summary>The component host's fully-qualified domain name. The identity service derives the
    /// stable component id from this plus <see cref="ComponentType"/>; it is not taken from a
    /// caller-supplied id.</summary>
    public string Fqdn { get; init; } = "";

    /// <summary>The client (mTLS) subject public key, DER-encoded as a SubjectPublicKeyInfo.</summary>
    public byte[] PublicKey { get; init; } = [];

    /// <summary>The server-TLS subject public key, DER-encoded as a SubjectPublicKeyInfo. When set,
    /// the identity service also issues a server certificate for the component's own TLS endpoint.</summary>
    public byte[] ServerPublicKey { get; init; } = [];

    /// <summary>The DNS name(s) the component serves on; become the server certificate's SAN.</summary>
    public IReadOnlyList<string> ServerDnsNames { get; init; } = [];

    /// <summary>The one-time enrollment token from the enrollment file, validated by the identity
    /// service's enrollment policy.</summary>
    public string Token { get; init; } = "";
}
