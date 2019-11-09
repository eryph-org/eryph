using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Haipa.IdentityDb
{   
    public class Config
    {        public static string GetConfigPath()
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                $"Haipa{Path.DirectorySeparatorChar}zero");

            var privateConfigPath = Path.Combine(configPath, "private");
            var clientsConfigPath = Path.Combine(privateConfigPath, "clients");

            if (!Directory.Exists(clientsConfigPath))
                Directory.CreateDirectory(clientsConfigPath);

            return clientsConfigPath;
        }
    }

}
