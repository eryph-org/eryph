using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;

namespace Eryph.Runtime.Zero.HttpSys;

[SupportedOSPlatform("windows")]
public class WindowsCryptoIOServices : ICryptoIOServices
{
    //could be static, no need to shuffle this, too as data is already machine encrypted
    private static readonly byte[] Entropy = { 53, 31, 23, 122, 34, 123, 01, 245, 129, 251, 30 };

    public async Task<AsymmetricCipherKeyPair> TryReadPrivateKeyFile(string privateKeyFile)
    {
        try
        {
            var encryptedKey = await File.ReadAllBytesAsync(privateKeyFile);
            using var decryptedData = new MemoryStream(
                ProtectedData.Unprotect(encryptedKey, Entropy, DataProtectionScope.LocalMachine));
            using var reader = new StreamReader(decryptedData);
            return (AsymmetricCipherKeyPair)new PemReader(reader).ReadObject();
        }
        catch (Exception)
        {
            //ignored
        }

        return null;
    }

    public async Task WritePrivateKeyFile(string privateKeyFile, AsymmetricCipherKeyPair keyPair)
    {
        using var memoryStream = new MemoryStream();
        await using var writer = new StreamWriter(memoryStream);
        var pem = new PemWriter(writer);
        pem.WriteObject(keyPair);
        await writer.FlushAsync();

        var protectedData = ProtectedData.Protect(
            memoryStream.ToArray(), Entropy, DataProtectionScope.LocalMachine);

        await File.WriteAllBytesAsync(privateKeyFile, protectedData);
    }
}