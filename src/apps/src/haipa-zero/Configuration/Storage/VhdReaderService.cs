using System;
using System.Collections.Generic;
using System.IO;
using Haipa.Configuration;
using Haipa.Configuration.Model;
using Haipa.StateDb.Model;
using Newtonsoft.Json;

namespace Haipa.Runtime.Zero.Configuration.Storage
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

    internal class StorageConfig
    {
        public string Id{ get; set; }
        public string StorageIdentifier { get; set; }
        public string DataStore { get; set; }
        public string Project { get; set; }
        public string Environment { get; set; }

        public StorageVhdConfig[] VirtualDisks { get; set; }
    }

    internal class StorageVhdConfig
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
        public DateTime LastSeen { get; set; }

    }
}