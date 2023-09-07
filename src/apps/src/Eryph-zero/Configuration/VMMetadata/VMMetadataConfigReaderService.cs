using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Eryph.Configuration;
using Eryph.Resources.Machines;

namespace Eryph.Runtime.Zero.Configuration.VMMetadata
{
    internal class VMMetadataConfigReaderService : IConfigReaderService<CatletMetadata>
    {
        public IEnumerable<CatletMetadata> GetConfig()
        {
            var configFiles = Directory.GetFiles(ZeroConfig.GetMetadataConfigPath(), "*.json");

            foreach (var configFile in configFiles)
            {
                var configContent = File.ReadAllText(configFile);
                var config = JsonSerializer.Deserialize<CatletMetadata>(configContent);
                yield return config;
            }
        }
    }
}