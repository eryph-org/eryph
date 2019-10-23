using System;
using System.Diagnostics;
using System.IO;
using Haipa.Modules.Hosting;
using SimpleInjector;

namespace Haipa.Runtime.Zero
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                $"Haipa{Path.DirectorySeparatorChar}zero");

            var privateConfigPath = Path.Combine(configPath, "private");
            var clientsConfigPath = Path.Combine(privateConfigPath, "clients");

            if (!Directory.Exists(clientsConfigPath))
                Directory.CreateDirectory(clientsConfigPath);

            File.WriteAllText(Path.Combine(clientsConfigPath, "system-client.json"), 
                "{ \"client_id\": \"system_client\", \"client_secret\" : \"388D45FA-B36B-4988-BA59-B187D329C207\" }");
            File.WriteAllText(Path.Combine(configPath, "zero_info"), 
                $"{{ \"process_id\": \"{Process.GetCurrentProcess().Id}\", \"url\" : \"http://localhost:62189\" }}");

            var container = new Container();
            container.Bootstrap(args);

            container.RunModuleHostService("haipa-zero");

            File.Delete(Path.Combine(configPath, "zero_info"));


        }
    }
}
