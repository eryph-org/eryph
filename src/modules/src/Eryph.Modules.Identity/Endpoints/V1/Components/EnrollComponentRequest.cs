using System.Collections.Generic;
using Eryph.Messages.Components;
using JetBrains.Annotations;

namespace Eryph.Modules.Identity.Endpoints.V1.Components;

/// <summary>
/// A component's request to enroll and obtain its certificates. The component generates its key pairs
/// locally and sends only the public keys, authorized by a one-time enrollment token from its
/// enrollment file. Binary values are base64-encoded.
/// </summary>
[PublicAPI]
public class EnrollComponentRequest
{
    /// <summary>The type of component being enrolled. The token is bound to this type.</summary>
    public ComponentType ComponentType { get; set; }

    /// <summary>
    /// The component host's fully-qualified domain name. The token is bound to this host, and the
    /// stable component id is derived server-side from it plus <see cref="ComponentType"/>.
    /// </summary>
    public required string Fqdn { get; set; }

    /// <summary>The client (mTLS) public key, base64-encoded SubjectPublicKeyInfo (DER).</summary>
    public required string PublicKey { get; set; }

    /// <summary>
    /// The server-TLS public key, base64-encoded SubjectPublicKeyInfo (DER). When set, the identity
    /// service also issues a server certificate for the component's own TLS endpoint.
    /// </summary>
    public string? ServerPublicKey { get; set; }

    /// <summary>The DNS name(s) the component serves on; become the server certificate's SAN.</summary>
    public IReadOnlyList<string>? ServerDnsNames { get; set; }

    /// <summary>The one-time enrollment token from the enrollment file.</summary>
    public required string Token { get; set; }
}
