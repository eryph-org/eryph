using System.Security.Cryptography;

namespace Eryph.Security.Cryptography;

public interface ICertificateKeyService
{
    RSA GenerateRsaKey(int keyLength);

    RSA GeneratePersistedRsaKey(string keyName, int keyLength);

    RSA? GetPersistedRsaKey(string keyName);

    void DeletePersistedKey(string keyName);
}
