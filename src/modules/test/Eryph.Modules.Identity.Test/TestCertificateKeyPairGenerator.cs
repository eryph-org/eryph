using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Eryph.Security.Cryptography;

namespace Eryph.Modules.Identity.Test;

internal class TestCertificateKeyPairGenerator : ICertificateKeyPairGenerator
{
    public RSA GenerateRsaKeyPair(int keyLength)
    {
        return RSA.Create(keyLength);
    }

    public RSA GenerateProtectedRsaKeyPair(int keyLength)
    {
        return RSA.Create(keyLength);
    }
}
