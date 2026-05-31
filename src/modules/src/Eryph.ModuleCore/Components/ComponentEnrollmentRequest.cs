using Eryph.Messages.Components;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// A component's request to enroll and obtain its mTLS client certificate. Sent over HTTPS to the
/// identity service (off the bus — the bus is what the resulting certificate secures). The
/// component generates its key pair locally; only the public key is sent, so the private key never
/// leaves the component.
/// </summary>
public sealed class ComponentEnrollmentRequest
{
    public ComponentType ComponentType { get; init; }

    /// <summary>The component host's fully-qualified domain name. The identity service derives the
    /// stable component id from this plus <see cref="ComponentType"/>; it is not taken from a
    /// caller-supplied id.</summary>
    public string Fqdn { get; init; } = "";

    /// <summary>The subject public key, DER-encoded as a SubjectPublicKeyInfo.</summary>
    public byte[] PublicKey { get; init; } = [];

    /// <summary>An opaque enrollment credential validated by the (pluggable) enrollment policy —
    /// e.g. an operator-provisioned shared secret for the in-repo default policy.</summary>
    public string Credential { get; init; } = "";
}
