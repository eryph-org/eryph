using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Eryph.Configuration;
using Eryph.Configuration.Model;

namespace Eryph.Runtime.Zero.Configuration.Clients
{
    internal class ClientConfigReaderService : IConfigReaderService<ClientConfigModel>
    {
        public IEnumerable<ClientConfigModel> GetConfig()
        {
            var configFiles = Directory.GetFiles(ZeroConfig.GetClientConfigPath(), "*.json");

            foreach (var configFile in configFiles)
            {
                var configContent = File.ReadAllText(configFile);
                var config = JsonSerializer.Deserialize<ClientConfigModel>(configContent);
                yield return config;
            }
        }
    }
}