using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;

namespace Eryph.Runtime.Zero.HttpSys;

public interface ICryptoIOServices
{
    Task<AsymmetricCipherKeyPair> TryReadPrivateKeyFile(string privateKeyFile);
    Task WritePrivateKeyFile(string privateKeyFile, AsymmetricCipherKeyPair keyPair);
}