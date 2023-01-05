using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Security;

namespace Eryph.Security.Cryptography;

public class RSAProvider : IRSAProvider
{
    public AsymmetricCipherKeyPair CreateRSAKeyPair(int keyLength)
    {
        var kpGenerator = new RsaKeyPairGenerator();

        // certificate strength 2048 bits
        kpGenerator.Init(new KeyGenerationParameters(
            new SecureRandom(new CryptoApiRandomGenerator()), keyLength));

        return kpGenerator.GenerateKeyPair();
        
    }
}