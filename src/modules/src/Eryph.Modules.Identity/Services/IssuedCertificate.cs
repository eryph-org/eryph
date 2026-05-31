using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// A certificate issued by the component CA, together with the intermediate CA certificate(s) the
/// holder must present alongside the leaf so a relying party can build a chain to the (separately
/// trusted) root. The root is excluded — it is the pre-distributed trust anchor.
/// </summary>
public sealed class IssuedCertificate
{
    /// <summary>The issued leaf certificate (public part; the holder pairs it with its own key).</summary>
    public required X509Certificate2 Leaf { get; init; }

    /// <summary>The issuing intermediate CA certificate(s), in leaf-to-root order, root excluded.</summary>
    public required IReadOnlyList<X509Certificate2> IssuingChain { get; init; }
}
