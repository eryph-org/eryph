using System.Security.Cryptography.X509Certificates;

namespace Eryph.Security.Cryptography;

/// <summary>
/// Helpers for making a certificate's private key usable by the Windows TLS stack (Schannel).
/// </summary>
public static class SchannelCertificate
{
    /// <summary>
    /// Re-imports a certificate together with its private key so the key can be used by Schannel
    /// for a TLS handshake. Schannel cannot use <i>ephemeral</i> keys — those produced by
    /// <see cref="System.Security.Cryptography.X509Certificates.X509Certificate2.CopyWithPrivateKey"/>
    /// or by importing a PKCS#12 with <see cref="X509KeyStorageFlags.EphemeralKeySet"/> — and fails
    /// the handshake with "the platform does not support ephemeral keys". Exporting to PKCS#12 and
    /// re-importing with <see cref="X509KeyStorageFlags.DefaultKeySet"/> binds the key into the user
    /// key store for the lifetime of the returned certificate (non-persisted: it is removed when the
    /// certificate is disposed / the process exits), which Schannel accepts. No elevation required.
    /// </summary>
    public static X509Certificate2 MakeUsable(X509Certificate2 certificateWithPrivateKey) =>
        X509CertificateLoader.LoadPkcs12(
            certificateWithPrivateKey.Export(X509ContentType.Pkcs12),
            password: null,
            keyStorageFlags: X509KeyStorageFlags.DefaultKeySet);
}
