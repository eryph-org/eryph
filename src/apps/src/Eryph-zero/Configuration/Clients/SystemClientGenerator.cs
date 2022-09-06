using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Security.Cryptography;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

namespace Eryph.Runtime.Zero.Configuration.Clients
{
    public static class SystemClientGenerator
    {
        public static async Task EnsureSystemClient(ICertificateGenerator certificateGenerator, ICryptoIOServices cryptoIOServices, Uri identityEndpoint)
        {
            var systemClientDataFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.json");
            var systemClientKeyFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.key");

            var recreateSystemClient = !File.Exists(systemClientDataFile) || !File.Exists(systemClientKeyFile);
            var entropy = Encoding.UTF8.GetBytes(identityEndpoint.ToString());

            AsymmetricKeyParameter publicKey = null;
            if (File.Exists(systemClientDataFile))
                try
                {
                    var systemClientData =
                        JsonSerializer.Deserialize<ClientConfigModel>(await File.ReadAllTextAsync(systemClientDataFile));
                    publicKey = systemClientData != null 
                        ? GetPublicKey(systemClientData.X509CertificateBase64)
                        : null;
                }
                catch (Exception)
                {
                    recreateSystemClient = true;
                }

            AsymmetricCipherKeyPair privateKeyPair = null;
            if (!recreateSystemClient && File.Exists(systemClientKeyFile))
                try
                {
                    
                    privateKeyPair = await cryptoIOServices.TryReadPrivateKeyFile(systemClientKeyFile, entropy);
                }
                catch (Exception)
                {
                    recreateSystemClient = true;
                }

            if (!recreateSystemClient && publicKey != null && privateKeyPair != null)
                if (privateKeyPair.Public.Equals(publicKey))
                    return;

            RemoveSystemClient();

            var (certificate, keyPair) = certificateGenerator.GenerateSelfSignedCertificate(
                new X509Name("CN=system-client"), 5*365, 2048);

            var newClient = new ClientConfigModel
            {
                ClientId = "system-client",
                X509CertificateBase64 = Convert.ToBase64String(certificate.GetEncoded()),
                AllowedScopes = new[] {"openid", "compute_api", "common_api", "identity:clients:write:all"}
            };

            var clientIO = new ConfigIO(ZeroConfig.GetClientConfigPath());
            await clientIO.SaveConfigFile(newClient, newClient.ClientId);
            await cryptoIOServices.WritePrivateKeyFile(systemClientKeyFile, keyPair, entropy);
        }

        private static void RemoveSystemClient()
        {
            var systemClientDataFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.json");
            var systemClientKeyFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.key");

            if (File.Exists(systemClientDataFile)) File.Delete(systemClientDataFile);

            if (File.Exists(systemClientKeyFile)) File.Delete(systemClientKeyFile);
        }

        private static AsymmetricKeyParameter GetPublicKey(string certData)
        {
            var parser = new X509CertificateParser();
            var cert2 = parser.ReadCertificate(Convert.FromBase64String(certData));
            return cert2.GetPublicKey();
        }
    }
}