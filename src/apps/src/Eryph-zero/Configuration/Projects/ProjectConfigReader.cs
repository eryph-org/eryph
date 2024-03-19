using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Resources.Machines;

namespace Eryph.Runtime.Zero.Configuration.Projects
{
    internal class ProjectConfigReader : IConfigReader<ProjectConfigModel>
    {
        private readonly string _configPath = ZeroConfig.GetProjectsConfigPath();

        public async IAsyncEnumerable<ProjectConfigModel> ReadAsync(CancellationToken cancellationToken = default)
        {
            foreach (var configFile in Directory.EnumerateFiles(_configPath, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var configContent = await File.ReadAllTextAsync(configFile, cancellationToken);
                var config = JsonSerializer.Deserialize<ProjectConfigModel>(configContent);
                yield return config;
            }
        }
    }
}
