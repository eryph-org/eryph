using System;
using System.IO;

namespace Eryph.App
{
    public class Config
    {
        public static string GetConfigPath(string module)
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                $"eryph{Path.DirectorySeparatorChar}{module}");

            return configPath;
        }


        public static void EnsurePath(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}