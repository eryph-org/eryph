namespace Eryph.ModuleCore.Components;

/// <summary>
/// The component's enrolled client certificate material as PEM-encoded strings. Used by consumers
/// that configure PEM-based TLS directly (e.g. OVN's <c>set-ssl</c>, which takes PEM strings rather
/// than a PKCS#12 file).
/// </summary>
/// <param name="PrivateKeyPem">The PKCS#8 private key (PEM).</param>
/// <param name="CertificatePem">The leaf certificate followed by its issuing chain (PEM).</param>
/// <param name="CaBundlePem">The CA trust bundle used to validate peers (PEM).</param>
public sealed record ComponentCertificatePem(
    string PrivateKeyPem,
    string CertificatePem,
    string CaBundlePem);
