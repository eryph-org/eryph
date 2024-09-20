using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Security.Cryptography;

public interface ICertificateKeyService
{
    public RSA GenerateRsaKey(int keyLength);

    public RSA GeneratePersistedRsaKey(string keyName, int keyLength);

    RSA? GetPersistedRsaKey(string keyName);
    
    void DeletePersistedKey(string keyName);
}
