using System;
using System.IO;
using Haipa.App;
using Haipa.Runtime.Zero.Configuration.Clients;
using Haipa.Security.Cryptography;

namespace Haipa.Runtime.Zero.Configuration
{
    public static class ZeroConfig
    {
        public static string GetConfigPath() => Config.GetConfigPath("zero");

        public static string GetClientConfigPath()
        {
            var privateConfigPath = Path.Combine(GetConfigPath(), "private");
            var clientsConfigPath = Path.Combine(privateConfigPath, "clients");

            return clientsConfigPath;
        }


        public static void EnsureConfiguration()
        {
            Config.EnsurePath(GetConfigPath());
            Config.EnsurePath(GetClientConfigPath());
            SystemClientGenerator.EnsureSystemClient();

            Certificate.CreateSSL(new CertificateOptions
            {
                Issuer = Network.FQDN,
                FriendlyName = "Haipa Zero Management Certificate",
                Suffix = "CA",
                ValidStartDate = DateTime.UtcNow,
                ValidEndDate = DateTime.UtcNow.AddYears(5),
                Password = "password",
                ExportDirectory = Directory.GetCurrentDirectory(),
                URL = "https://localhost:62189/",
                AppID = "9412ee86-c21b-4eb8-bd89-f650fbf44931",
                CACertName = "HaipaCA.pfx"
            });
        }
    }
}
