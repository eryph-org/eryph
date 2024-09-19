using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Eryph.Modules.Identity.Services;

public interface ISigningCertificateManager
{
    X509Certificate2 GetSigningCertificate();

    X509Certificate2 GetEncryptionCertificate();
}
