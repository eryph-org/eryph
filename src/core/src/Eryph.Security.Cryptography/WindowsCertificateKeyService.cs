using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Security.Cryptography;

[SupportedOSPlatform("windows")]
public class WindowsCertificateKeyService : ICertificateKeyService
{
    public RSA GenerateRsaKey(int keyLength)
    {
        return RSA.Create(keyLength);
    }

    public RSA GeneratePersistedRsaKey(string keyName, int keyLength)
    {
        using var cngKey = CngKey.Create(
            CngAlgorithm.Rsa,
            keyName,
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

    public RSA? GetPersistedRsaKey(string keyName)
    {
        if (!CngKey.Exists(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.MachineKey))
            return null;

        using var cngKey = CngKey.Open(
            keyName,
            CngProvider.MicrosoftSoftwareKeyStorageProvider,
            CngKeyOpenOptions.MachineKey);
        return new RSACng(cngKey);
    }

    public void DeletePersistedKey(string keyName)
    {
        if (!CngKey.Exists(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.MachineKey))
            return;

        using var cngKey = CngKey.Open(
            keyName,
            CngProvider.MicrosoftSoftwareKeyStorageProvider,
            CngKeyOpenOptions.MachineKey);
        cngKey.Delete();
    }
}
