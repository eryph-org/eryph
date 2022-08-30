using System;
using System.IO;
using System.Text.Json;
using Eryph.Configuration.Model;
using Eryph.Security.Cryptography;
using Org.BouncyCastle.Crypto;

namespace Eryph.Runtime.Zero.Configuration.Clients
{
    public static class SystemClientGenerator
    {
        public static void EnsureSystemClient()
        {
            var systemClientDataFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.json");
            var systemClientKeyFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.key");

            var recreateSystemClient = !File.Exists(systemClientDataFile) || !File.Exists(systemClientKeyFile);

            AsymmetricKeyParameter publicKey = null;
            if (File.Exists(systemClientDataFile))
                try
                {
                    var systemClientData =
                        JsonSerializer.Deserialize<ClientConfigModel>(File.ReadAllText(systemClientDataFile));
                    publicKey = CertHelper.GetPublicKey(systemClientData.X509CertificateBase64);
                }
                catch (Exception)
                {
                    recreateSystemClient = true;
                }

            AsymmetricCipherKeyPair privateKeyPair = null;
            if (!recreateSystemClient && File.Exists(systemClientKeyFile))
                try
                {
                    privateKeyPair = CertHelper.ReadPrivateKeyFile(systemClientKeyFile);
                }
                catch (Exception)
                {
                    recreateSystemClient = true;
                }

            if (!recreateSystemClient && publicKey != null && privateKeyPair != null)
                if (privateKeyPair.Public.Equals(publicKey))
                    return;

            RemoveSystemClient();

            var (certificate, keyPair) = X509Generation.GenerateSelfSignedCertificate("system-client");

            var newClient = new ClientConfigModel
            {
                ClientId = "system-client",
                X509CertificateBase64 = Convert.ToBase64String(certificate.GetEncoded()),
                AllowedScopes = new[] {"openid", "compute_api", "common_api", "identity:clients:write:all"}
            };

            var clientIO = new ConfigIO(ZeroConfig.GetClientConfigPath());
            clientIO.SaveConfigFile(newClient, newClient.ClientId);
            CertHelper.WritePrivateKeyFile(systemClientKeyFile, keyPair);
        }

        private static void RemoveSystemClient()
        {
            var systemClientDataFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.json");
            var systemClientKeyFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.key");

            if (File.Exists(systemClientDataFile)) File.Delete(systemClientDataFile);

            if (File.Exists(systemClientKeyFile)) File.Delete(systemClientKeyFile);
        }
    }
}