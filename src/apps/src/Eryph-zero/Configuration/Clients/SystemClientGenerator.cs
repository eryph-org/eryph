using System;
using System.IO;
using Eryph.Configuration.Model;
using Eryph.Security.Cryptography;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;

namespace Eryph.Runtime.Zero.Configuration.Clients
{
    public static class SystemClientGenerator
    {
        public static void EnsureSystemClient(ICertificateGenerator certificateGenerator)
        {
            var systemClientDataFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.json");
            var systemClientKeyFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.key");

            var recreateSystemClient = !File.Exists(systemClientDataFile) || !File.Exists(systemClientKeyFile);

            AsymmetricKeyParameter publicKey = null;
            if (File.Exists(systemClientDataFile))
                try
                {
                    var systemClientData =
                        JsonConvert.DeserializeObject<ClientConfigModel>(File.ReadAllText(systemClientDataFile));
                    publicKey = GetPublicKey(systemClientData.X509CertificateBase64);
                }
                catch (Exception)
                {
                    recreateSystemClient = true;
                }

            AsymmetricCipherKeyPair privateKeyPair = null;
            if (!recreateSystemClient && File.Exists(systemClientKeyFile))
                try
                {
                    privateKeyPair = ReadPrivateKeyFile(systemClientKeyFile);
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
            clientIO.SaveConfigFile(newClient, newClient.ClientId);
            WritePrivateKeyFile(systemClientKeyFile, keyPair);
        }

        private static void RemoveSystemClient()
        {
            var systemClientDataFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.json");
            var systemClientKeyFile = Path.Combine(ZeroConfig.GetClientConfigPath(), "system-client.key");

            if (File.Exists(systemClientDataFile)) File.Delete(systemClientDataFile);

            if (File.Exists(systemClientKeyFile)) File.Delete(systemClientKeyFile);
        }

        private static AsymmetricCipherKeyPair ReadPrivateKeyFile(string filepath)
        {
            using var reader = File.OpenText(filepath);
            return (AsymmetricCipherKeyPair) new PemReader(reader).ReadObject();
        }

        private static void WritePrivateKeyFile(string filepath, AsymmetricCipherKeyPair keyPair)
        {
            using var writer = new StreamWriter(filepath);
            new PemWriter(writer).WriteObject(keyPair);
        }

        private static AsymmetricKeyParameter GetPublicKey(string certData)
        {
            var parser = new X509CertificateParser();
            var cert2 = parser.ReadCertificate(Convert.FromBase64String(certData));
            return cert2.GetPublicKey();
        }
    }
}