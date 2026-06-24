using System.Security.Cryptography.X509Certificates;

namespace Eryph.Modules.Identity.Services;

public interface ITokenCertificateManager
{
    X509Certificate2 GetEncryptionCertificate();

    X509Certificate2 GetSigningCertificate();
}
