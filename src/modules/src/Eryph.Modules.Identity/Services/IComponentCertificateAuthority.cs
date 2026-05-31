using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// The deployment's component certificate authority. Manages the long-lived CA certificate
/// (created on first use, like the token-signing certificate) and issues per-component client
/// certificates used to authenticate components on the message bus via mTLS. The CA certificate
/// is the trust anchor distributed to components so they can validate each other and the broker.
/// </summary>
public interface IComponentCertificateAuthority
{
    /// <summary>
    /// The component CA certificate including its private key (used to sign issued component
    /// certificates). Created on first use and reused afterwards.
    /// </summary>
    X509Certificate2 GetCaCertificate();

    /// <summary>
    /// Issues a component client certificate for the given component identity, signed by the
    /// component CA. The returned certificate carries only the public key; the component holds
    /// the matching private key (generated during enrollment).
    /// </summary>
    /// <param name="componentId">The component's stable identifier, carried as a URI SAN
    /// (<c>urn:eryph:component:{componentId}</c>) so the authenticated identity is unambiguous.</param>
    /// <param name="fqdn">The component host's fully-qualified domain name (subject CN + DNS SAN).</param>
    /// <param name="subjectPublicKey">The component's public key, taken from its enrollment request.</param>
    /// <param name="validDays">Requested validity in days; clamped to the CA's remaining lifetime.</param>
    X509Certificate2 IssueComponentCertificate(
        string componentId,
        string fqdn,
        RSA subjectPublicKey,
        int validDays = 365);
}
