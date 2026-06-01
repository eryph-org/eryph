using System.Collections.Generic;
using JetBrains.Annotations;

namespace Eryph.Modules.Identity.Models.V1;

/// <summary>
/// The result of a successful component enrollment: the issued certificates and the CA trust bundle.
/// All certificate values are base64-encoded DER (public parts only — private keys never leave the
/// component). Server-certificate fields are empty when no server key was requested.
/// </summary>
[PublicAPI]
public class EnrolledComponent
{
    /// <summary>The stable component id the certificate was issued for (derived server-side).</summary>
    public required string ComponentId { get; set; }

    /// <summary>The issued client (mTLS) leaf certificate.</summary>
    public required string Certificate { get; set; }

    /// <summary>The client certificate's issuing intermediate CA certificate(s), to present alongside the leaf.</summary>
    public required IReadOnlyList<string> IssuingChain { get; set; }

    /// <summary>The issued server-TLS leaf certificate; empty when none was requested.</summary>
    public required string ServerCertificate { get; set; }

    /// <summary>The server certificate's issuing intermediate CA certificate(s); empty when none.</summary>
    public required IReadOnlyList<string> ServerIssuingChain { get; set; }

    /// <summary>The CA trust bundle: the currently trusted root CA certificate(s).</summary>
    public required IReadOnlyList<string> CaTrustBundle { get; set; }
}
