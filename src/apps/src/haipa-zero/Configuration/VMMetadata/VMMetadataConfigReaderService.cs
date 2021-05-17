using System.Collections.Generic;
using System.IO;
using Haipa.Configuration;
using Haipa.VmConfig;
using Newtonsoft.Json;

namespace Haipa.Runtime.Zero.Configuration.VMMetadata
{
    internal class VMMetadataConfigReaderService : IConfigReaderService<VirtualMachineMetadata>
    {
        public IEnumerable<VirtualMachineMetadata> GetConfig()
        {
            var configFiles = Directory.GetFiles(ZeroConfig.GetMetadataConfigPath(), "*.json");

            foreach (var configFile in configFiles)
            {
                var configContent = File.ReadAllText(configFile);
                var config = JsonConvert.DeserializeObject<VirtualMachineMetadata>(configContent);
                yield return config;
            }
        }
    }
}