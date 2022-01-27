using System.Collections.Generic;
using System.IO;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.StateDb.Model;
using Newtonsoft.Json;

namespace Eryph.Runtime.Zero.Configuration.Storage
{
    internal class VhdReaderService : IConfigReaderService<VirtualDisk>
    {
        public IEnumerable<VirtualDisk> GetConfig()
        {
            var configFiles = Directory.GetFiles(ZeroConfig.GetStorageConfigPath(), "*.json");

            foreach (var configFile in configFiles)
            {
                var configContent = File.ReadAllText(configFile);
                var config = JsonConvert.DeserializeObject<StorageConfig>(configContent);
                
                if(config == null)
                    continue;
                
                foreach (var virtualDiskConfig in config.VirtualDisks)
                {
                    yield return new VirtualDisk
                    {
                        Id = virtualDiskConfig.Id,
                        Name = virtualDiskConfig.Name,
                        DataStore = config.DataStore,
                        Environment = config.Environment,
                        Project = config.Project,
                        StorageIdentifier = config.StorageIdentifier
                    };
                }
            }
        }
    }
}