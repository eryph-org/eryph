using System;
using System.Collections.Generic;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// The result of a successful enrollment: the issued leaf certificate plus the CA trust bundle the
/// component should trust (each DER-encoded). The component pairs the leaf with the private key it
/// holds locally and uses the bundle to validate the broker and its peers.
/// </summary>
public sealed class ComponentEnrollmentResult
{
    /// <summary>The stable component id the certificate was issued for (derived server-side).</summary>
    public Guid ComponentId { get; init; }

    /// <summary>The issued leaf certificate, DER-encoded (public part only).</summary>
    public byte[] Certificate { get; init; } = [];

    /// <summary>The issuing intermediate CA certificate(s), each DER-encoded, that the component
    /// presents alongside its leaf so a relying party can build a chain to a trusted root.</summary>
    public IReadOnlyList<byte[]> IssuingChain { get; init; } = [];

    /// <summary>The CA trust bundle: the currently trusted root CA certificate(s), each
    /// DER-encoded. A bundle rather than a single anchor so a CA rollover can keep old and new
    /// roots trusted at once.</summary>
    public IReadOnlyList<byte[]> CaTrustBundle { get; init; } = [];
}
