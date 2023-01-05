using Org.BouncyCastle.Crypto;

namespace Eryph.Security.Cryptography;

public interface IRSAProvider
{
    AsymmetricCipherKeyPair CreateRSAKeyPair(int keyLength);
}