using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;

namespace Eryph.Core
{
    public class Config
    {
        public static string GetConfigPath(string module)
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                $"eryph{Path.DirectorySeparatorChar}{module}");

            return configPath;
        }


        [SupportedOSPlatform("windows")]
        public static void EnsurePath(string path, DirectorySecurity security)
        {
            var directoryInfo = new DirectoryInfo(path);
            if (!directoryInfo.Exists)
                directoryInfo.Create(security);
            else
            {
                directoryInfo.SetAccessControl(security);
            }

        }

        public static void EnsurePath(string path)
        {
            var directoryInfo = new DirectoryInfo(path);
            if (!directoryInfo.Exists)
                directoryInfo.Create();

        }
    }
}