using System.Security.Cryptography.X509Certificates;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Persists and inspects the component's enrolled certificate material (leaf, private key, issuing
/// chain and CA trust bundle). The bus mTLS transport reads the client certificate file via
/// <see cref="GetClientCertificatePfxPath"/>; enrollment/renewal writes to it. Abstracted so the
/// enrollment client is testable without touching the file system.
/// </summary>
public interface IComponentCertificateStore
{
    /// <summary>
    /// True when a usable certificate is stored and is not yet within its renewal window — i.e.
    /// enrollment/renewal is not currently needed.
    /// </summary>
    bool HasCurrentCertificate();

    /// <summary>
    /// True when a certificate is stored and still valid (even if within the renewal window) — i.e.
    /// the component can still connect while a renewal is attempted.
    /// </summary>
    bool HasValidCertificate();

    /// <summary>Persists the enrolled client + server certificates, their PKCS#8 private keys, chains
    /// and the CA trust bundle. The server certificate is optional (it is written only when present).</summary>
    void Save(byte[] clientPkcs8PrivateKey, byte[] serverPkcs8PrivateKey, ComponentEnrollmentResult result);

    /// <summary>
    /// The component's mTLS client certificate as the leaf plus its private key, usable for TLS, or
    /// <see langword="null"/> when not enrolled. This is the leaf only; the issuing chain is carried by
    /// the PKCS#12 at <see cref="GetClientCertificatePfxPath"/> (what the bus transport actually uses).
    /// </summary>
    X509Certificate2? LoadClientCertificate();

    /// <summary>
    /// Path to the client certificate as a PKCS#12 file (what the RabbitMQ transport hands to the
    /// TLS stack — Rebus' SslSettings consumes a certificate file, not an in-memory certificate), or
    /// <see langword="null"/> when not enrolled.
    /// </summary>
    string? GetClientCertificatePfxPath();

    /// <summary>The trusted CA roots (the bundle) used to validate the broker and peers.</summary>
    X509Certificate2Collection LoadCaTrustBundle();

    /// <summary>
    /// The enrolled client PEM material (private key, leaf-with-issuing-chain certificate and CA trust
    /// bundle) as strings, or <see langword="null"/> when not enrolled. For consumers that configure
    /// PEM-based TLS directly (e.g. OVN's <c>set-ssl</c>, which takes PEM strings, not a PKCS#12 file).
    /// </summary>
    ComponentCertificatePem? ReadClientCertificatePem();
}
