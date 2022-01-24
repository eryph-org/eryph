using System.Collections.Generic;
using System.IO;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Newtonsoft.Json;

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
                var config = JsonConvert.DeserializeObject<ClientConfigModel>(configContent);
                yield return config;
            }
        }
    }
}