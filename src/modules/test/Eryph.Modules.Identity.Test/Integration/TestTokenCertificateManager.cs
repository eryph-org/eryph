using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Eryph.Modules.Identity.Services;

namespace Eryph.Modules.Identity.Test.Integration;

public class TestTokenCertificateManager(
    TokenCertificateFixture tokenCertificates)
    : ITokenCertificateManager
{
    public X509Certificate2 GetEncryptionCertificate() =>
        tokenCertificates.EncryptionCertificate;


    public X509Certificate2 GetSigningCertificate() =>
        tokenCertificates.SigningCertificate;
}
