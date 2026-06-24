using System;
using System.Security.Cryptography;
using Eryph.Security.Cryptography;

namespace Eryph.Modules.Identity.Test.Integration;

internal class TestCertificateKeyService : ICertificateKeyService
{
    public RSA GenerateRsaKey(int keyLength)
    {
        return RSA.Create(keyLength);
    }

    public RSA GeneratePersistedRsaKey(string keyName, int keyLength)
    {
        return RSA.Create(keyLength);
    }

    public RSA GetPersistedRsaKey(string keyName)
    {
        throw new NotImplementedException();
    }

    public void DeletePersistedKey(string keyName)
    {
        throw new NotImplementedException();
    }
}
