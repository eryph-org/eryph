using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Networks;
using Eryph.Configuration.Model;
using Eryph.Core;

namespace Eryph.Runtime.Zero.Configuration.Projects
{
    internal class ProjectNetworksReader : IConfigReader<ProjectNetworksConfig>
    {
        private readonly string _configPath = Path.Combine(ZeroConfig.GetProjectNetworksConfigPath());

        public async IAsyncEnumerable<ProjectNetworksConfig> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var configFile in Directory.EnumerateFiles(_configPath, "*.json"))
            {
                var configContent = await File.ReadAllTextAsync(configFile, cancellationToken);
                var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(configContent);
                if (configDictionary is null)
                    continue;

                var config = ProjectNetworksConfigDictionaryConverter.Convert(configDictionary);
                yield return config;
            }
        }
    }
}
