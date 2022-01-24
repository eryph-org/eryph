using System.Collections.Generic;
using System.IO;
using Eryph.Configuration;
using Eryph.Resources.Machines;
using Newtonsoft.Json;

namespace Eryph.Runtime.Zero.Configuration.VMMetadata
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