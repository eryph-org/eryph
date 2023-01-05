using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Eryph.Configuration;
using Eryph.StateDb.Model;

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
                var config = JsonSerializer.Deserialize<StorageConfig>(configContent);
                
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
                        StorageIdentifier = config.StorageIdentifier
                    };
                }
            }
        }
    }
}