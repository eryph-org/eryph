using System;
using System.IO;

namespace Haipa.App
{
    public class Config
    {
        public static string GetConfigPath(string module)
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                $"haipa{Path.DirectorySeparatorChar}{module}");

            return configPath;
        }


        public static void EnsurePath(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}