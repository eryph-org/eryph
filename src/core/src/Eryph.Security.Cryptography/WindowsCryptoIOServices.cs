using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Security.Cryptography;

[SupportedOSPlatform("windows")]
public class WindowsCryptoIOServices : ICryptoIOServices
{
    //could be static, no need to shuffle this, too as data is already machine encrypted

    public async Task<RSA?> TryReadPrivateKeyFile(string privateKeyFile, byte[] entropy)
    {
        try
        {
            if(!File.Exists(privateKeyFile))
                return null;

            var protectedData = await File.ReadAllBytesAsync(privateKeyFile);
            var data = ProtectedData.Unprotect(protectedData, entropy, DataProtectionScope.LocalMachine);
            var key = RSA.Create();
            key.ImportFromPem(Encoding.UTF8.GetString(data));
            
            return key;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task WritePrivateKeyFile(string privateKeyFile, RSA keyPair, byte[] entropy)
    {
        var data = Encoding.UTF8.GetBytes(keyPair.ExportRSAPrivateKeyPem());
        var protectedData = ProtectedData.Protect(data, entropy, DataProtectionScope.LocalMachine);

        await File.WriteAllBytesAsync(privateKeyFile, protectedData);
    }
}
