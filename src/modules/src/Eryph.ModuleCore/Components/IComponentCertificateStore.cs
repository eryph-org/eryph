using System;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Persists and inspects the component's enrolled certificate material (leaf, private key, issuing
/// chain and CA trust bundle). The bus mTLS transport reads from here; enrollment/renewal writes
/// to it. Abstracted so the enrollment client is testable without touching the file system.
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

    /// <summary>Persists the enrolled certificate, its PKCS#8 private key, chain and trust bundle.</summary>
    void Save(byte[] pkcs8PrivateKey, ComponentEnrollmentResult result);
}
