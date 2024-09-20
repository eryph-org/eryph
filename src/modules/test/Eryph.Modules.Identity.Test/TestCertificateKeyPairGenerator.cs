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

    public RSA GeneratePersistedRsaKeyPair(string keyName, int keyLength)
    {
        throw new NotImplementedException();
    }

    public RSA GetPersistedRsaKeyPair(string keyName)
    {
        throw new NotImplementedException();
    }

    public void DeletePersistedKey(string keyName)
    {
        throw new NotImplementedException();
    }

    public RSA GenerateProtectedRsaKeyPair(string keyName, int keyLength)
    {
        return RSA.Create(keyLength);
    }
}
