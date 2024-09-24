using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Eryph.Modules.Identity.Services;

public interface ITokenCertificateManager
{
    X509Certificate2 GetEncryptionCertificate();

    X509Certificate2 GetSigningCertificate();
}
