using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Eryph.Security.Cryptography;

namespace Eryph.Modules.Identity.Test;

internal class TestCertificateKeyService : ICertificateKeyService
{
    public RSA GenerateRsaKey(int keyLength)
    {
        return RSA.Create(keyLength);
    }

    public RSA GeneratePersistedRsaKey(string keyName, int keyLength)
    {
        throw new NotImplementedException();
    }

    public RSA GetPersistedRsaKey(string keyName)
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
