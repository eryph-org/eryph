using System;
using System.IO;

namespace Haipa.Runtime.Zero.ConfigStore
{   
    public class Config
    {

        public static string GetConfigPath(string module)
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                $"haipa{Path.DirectorySeparatorChar}{module}");

            return configPath;
        }

        public static string GetClientConfigPath()
        {
            var privateConfigPath = Path.Combine(GetConfigPath("identity"), "private");
            var clientsConfigPath = Path.Combine(privateConfigPath, "clients");

            return clientsConfigPath;
        }

        public static void EnsureConfigPaths()
        {
            EnsurePath(GetConfigPath("identity")); 
            EnsurePath(GetConfigPath("zero"));
            EnsurePath(GetConfigPath("api"));
            EnsurePath(GetClientConfigPath());            
        }

        private static void EnsurePath(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

        }

    }


}
