using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Security.Cryptography;

public interface ICertificateKeyGenerator
{
    public RSA GenerateRsaKeyPair(int keyLength, bool isProtected);
}