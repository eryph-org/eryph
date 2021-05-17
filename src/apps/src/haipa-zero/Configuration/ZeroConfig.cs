using System;
using System.IO;
using Haipa.App;
using Haipa.Runtime.Zero.Configuration.Clients;
using Haipa.Security.Cryptography;

namespace Haipa.Runtime.Zero.Configuration
{
    public static class ZeroConfig
    {
        public static string GetConfigPath()
        {
            return Config.GetConfigPath("zero");
        }

        public static string GetClientConfigPath()
        {
            var privateConfigPath = Path.Combine(GetConfigPath(), "private");
            var clientsConfigPath = Path.Combine(privateConfigPath, "clients");

            return clientsConfigPath;
        }

        public static string GetVMConfigPath()
        {
            var privateConfigPath = Path.Combine(GetConfigPath(), "private");
            var vmConfigPath = Path.Combine(privateConfigPath, "vms");

            return vmConfigPath;
        }

        public static string GetMetadataConfigPath()
        {
            var vmConfigPath = GetVMConfigPath();
            var metadataConfigPath = Path.Combine(vmConfigPath, "md");

            return metadataConfigPath;
        }


        public static void EnsureConfiguration()
        {
            Config.EnsurePath(GetConfigPath());
            Config.EnsurePath(GetClientConfigPath());
            Config.EnsurePath(GetVMConfigPath());
            Config.EnsurePath(GetMetadataConfigPath());

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