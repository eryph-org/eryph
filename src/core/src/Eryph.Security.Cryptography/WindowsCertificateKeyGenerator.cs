using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Security.Cryptography;

[SupportedOSPlatform("windows")]
public class WindowsCertificateKeyGenerator : ICertificateKeyGenerator
{
    public RSA GenerateRsaKeyPair(int keyLength, bool isProtected)
    {
        using var cngKey = CngKey.Create(
            CngAlgorithm.Rsa,
            Guid.NewGuid().ToString(),
            new CngKeyCreationParameters
            {
                KeyCreationOptions = CngKeyCreationOptions.MachineKey,
                ExportPolicy = CngExportPolicies.None,
                Parameters =
                {
                    new CngProperty("Length", BitConverter.GetBytes(keyLength), CngPropertyOptions.None)
                }
            });
        
        return new RSACng(cngKey);
    }
}