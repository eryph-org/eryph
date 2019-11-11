using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Haipa.Runtime.Zero.ConfigStore.Clients
{
    internal class ClientConfigReaderService : IConfigReaderService<ClientConfigModel>
    {
        public IEnumerable<ClientConfigModel> GetConfig()
        {
            var configFiles = Directory.GetFiles(Config.GetConfigPath(), "*.json");

            foreach (var configFile in configFiles)
            {
                var configContent = File.ReadAllText(configFile);
                var config = JsonConvert.DeserializeObject<ClientConfigModel>(configContent);
                yield return config;
            }
        }
    }

    internal class ClientConfigWriterService : IConfigWriterService<ClientConfigModel>
    {
        public Task Delete(ClientConfigModel client)
        {
            client.DeleteConfigFile();
            return Task.CompletedTask;
        }

        public Task Update(ClientConfigModel client)
        {
            return client.SaveConfigFile();

        }

        public Task Add(ClientConfigModel client)
        {
            return client.SaveConfigFile();
        }
    }
}