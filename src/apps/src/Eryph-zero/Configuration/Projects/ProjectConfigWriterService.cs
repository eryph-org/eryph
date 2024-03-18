using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using AutoMapper;
using Eryph.Configuration;
using Eryph.Configuration.Model;
using Eryph.StateDb.Model;

namespace Eryph.Runtime.Zero.Configuration.Projects
{
    internal class ProjectConfigWriterService : ISimpleConfigWriter<Project>, IConfigReaderService<Project>
    {
        private readonly IMapper _mapper;
        private readonly string _configPath;

        public ProjectConfigWriterService(
            IMapper mapper,
            string configPath)
        {
            _mapper = mapper;
            _configPath = configPath;
        }

        public async Task Add(Project config)
        {
            var configModel = _mapper.Map<ProjectConfigModel>(config);
            var json = JsonSerializer.Serialize(configModel);
            await File.WriteAllTextAsync(Path.Combine(_configPath, $"{config.Id}.json"), json, Encoding.UTF8);
        }

        public Task Delete(Project config)
        {
            File.Delete(Path.Combine(_configPath, $"{config.Id}.json"));
            return Task.CompletedTask;
        }

        public async Task Update(Project config)
        {
            var configModel = _mapper.Map<ProjectConfigModel>(config);
            var json = JsonSerializer.Serialize(configModel);
            await File.WriteAllTextAsync(Path.Combine(_configPath, $"{config.Id}.json"), json, Encoding.UTF8);
        }

        public IEnumerable<Project> GetConfig()
        {
            var configFiles = Directory.GetFiles(ZeroConfig.GetMetadataConfigPath(), "*.json");

            foreach (var configFile in configFiles)
            {
                var json = File.ReadAllText(configFile);
                var config = JsonSerializer.Deserialize<ProjectConfigModel>(json);
                yield return _mapper.Map<Project>(config);
            }
        }
    }
}
