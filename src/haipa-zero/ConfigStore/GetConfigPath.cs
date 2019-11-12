using System;
using System.IO;
using Haipa.Runtime.Zero.ConfigStore.Clients;
using Haipa.Security.Cryptography;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.IsisMtt.Ocsp;
using Org.BouncyCastle.Crypto;

namespace Haipa.Runtime.Zero.ConfigStore
{   
    public class Config
    {

        public static string GetConfigPath()
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                $"Haipa{Path.DirectorySeparatorChar}zero");

            return configPath;
        }

        public static string GetClientConfigPath()
        {
            var privateConfigPath = Path.Combine(GetConfigPath(), "private");
            var clientsConfigPath = Path.Combine(privateConfigPath, "clients");

            return clientsConfigPath;
        }

        public static void EnsureConfigPaths()
        {
            EnsurePath(GetConfigPath());
            EnsurePath(GetClientConfigPath());            
        }

        private static void EnsurePath(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

        }

        public static void EnsureSystemClient()
        {
            var systemClientDataFile = Path.Combine(GetClientConfigPath(), "system-client.json");
            var systemClientKeyFile = Path.Combine(GetClientConfigPath(), "system-client.key");

            var recreateSystemClient = !File.Exists(systemClientDataFile) || !File.Exists(systemClientKeyFile);

            ClientConfigModel systemClientData = null;
            if (File.Exists(systemClientDataFile))
            {
                try
                {
                    systemClientData =
                        JsonConvert.DeserializeObject<ClientConfigModel>(File.ReadAllText(systemClientDataFile));

                }
                catch (Exception)
                {
                    recreateSystemClient = true;
                }
            }

            AsymmetricCipherKeyPair privateKey = null;
            if (File.Exists(systemClientKeyFile))
            {
                try
                {
                    privateKey = CertHelper.ReadPrivateKeyFile(systemClientKeyFile);
                }
                catch (Exception)
                {
                    recreateSystemClient = true;
                }
            }

            if (!recreateSystemClient)
            {
                return;
            }

            RemoveSystemClient();

            var(certificate, keyPair) = X509Generation.GenerateCertificate("system-client");

            systemClientData = new ClientConfigModel
            {
                ClientId = "system-client", X509CertificateBase64 = Convert.ToBase64String(certificate.GetEncoded())
            };
            systemClientData.SaveConfigFile();
            CertHelper.WritePrivateKeyFile(systemClientKeyFile, keyPair);

        }

        private static void RemoveSystemClient()
        {
            var systemClientDataFile = Path.Combine(GetClientConfigPath(), "system-client.json");
            var systemClientKeyFile = Path.Combine(GetClientConfigPath(), "system-client.key");

            if (File.Exists(systemClientDataFile))
            {
                File.Delete(systemClientDataFile);
            }

            if (File.Exists(systemClientKeyFile))
            {
                File.Delete(systemClientKeyFile);
            }
        }
    }
}
