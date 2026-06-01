using System.Security.Cryptography.X509Certificates;
using Eryph.Security.Cryptography;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Default <see cref="IServerCertificateProvider"/>: issues the server-TLS certificate from the
/// component CA's server intermediate, so it chains to the root components already trust. The key
/// is generated per process (the certificate is presented in-process and re-issued on restart),
/// avoiding any persisted server key.
/// </summary>
public sealed class CaServerCertificateProvider(
    ICertificateKeyService certificateKeyService,
    IComponentCertificateAuthority certificateAuthority)
    : IServerCertificateProvider
{
    public IssuedCertificate GetServerCertificate(string dnsName)
    {
        // CopyWithPrivateKey binds an independent copy of the key into the returned certificate, so
        // the source key can be disposed once the bound leaf has been created.
        using var key = certificateKeyService.GenerateRsaKey(2048);
        var issued = certificateAuthority.IssueServerCertificate([dnsName], key);

        // The server presents the leaf, so it needs the private key bound; the issuing chain is
        // carried through unchanged for the listener to present alongside it. The key is re-imported
        // so it is usable by Schannel — a CopyWithPrivateKey (ephemeral) key fails the TLS handshake
        // on Windows (see SchannelCertificate.MakeUsable).
        using var bound = issued.Leaf.CopyWithPrivateKey(key);
        return new IssuedCertificate
        {
            Leaf = SchannelCertificate.MakeUsable(bound),
            IssuingChain = issued.IssuingChain,
        };
    }
}
