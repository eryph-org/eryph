using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Eryph.Security.Cryptography;

public interface ICryptoIOServices
{
    Task<RSA?> TryReadPrivateKeyFile(string privateKeyFile, byte[] entropy);
    
    Task WritePrivateKeyFile(string privateKeyFile, RSA keyPair, byte[] entropy);
}
