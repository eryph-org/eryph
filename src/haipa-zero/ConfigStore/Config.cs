using System;
using System.IO;

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

    }
}
