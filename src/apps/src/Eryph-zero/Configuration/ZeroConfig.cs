using System;
using System.IO;
using Eryph.App;
using Eryph.Core;
using Eryph.Runtime.Zero.Configuration.Clients;
using Eryph.Security.Cryptography;

namespace Eryph.Runtime.Zero.Configuration
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

        public static string GetStorageConfigPath()
        {
            var vmConfigPath = GetVMConfigPath();
            var metadataConfigPath = Path.Combine(vmConfigPath, "storage");

            return metadataConfigPath;
        }
        
        public static string GetPrivateConfigPath()
        {
            return Path.Combine(GetConfigPath(), "private");;
        }


        public static void EnsureConfiguration()
        {
            Config.EnsurePath(GetConfigPath());
            Config.EnsurePath(GetClientConfigPath());
            Config.EnsurePath(GetVMConfigPath());
            Config.EnsurePath(GetMetadataConfigPath());
            Config.EnsurePath(GetStorageConfigPath());


        }
    }
}