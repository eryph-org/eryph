using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;

namespace Eryph.Runtime.Zero.HttpSys;

public interface ICryptoIOServices
{
    Task<AsymmetricCipherKeyPair> TryReadPrivateKeyFile(string privateKeyFile, byte[] entropy);
    Task WritePrivateKeyFile(string privateKeyFile, AsymmetricCipherKeyPair keyPair, byte[] entropy);
}