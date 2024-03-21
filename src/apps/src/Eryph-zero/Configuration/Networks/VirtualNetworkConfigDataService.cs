using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Eryph.Configuration.Model;
using Eryph.StateDb.Model;

namespace Eryph.Runtime.Zero.Configuration.Networks
{
    internal class VirtualNetworkConfigDataService : IConfigWriter<VirtualNetwork>
    {
        private readonly IConfigurationProvider _mapperConfig;
        private readonly string _configPath;

        public VirtualNetworkConfigDataService(IConfigurationProvider mapperConfig)
        {
            _mapperConfig = mapperConfig;
            _configPath = ZeroConfig.GetProjectsConfigPath();
        }

        public Task AddAsync(VirtualNetwork entity, CancellationToken cancellationToken = default)
        {
            return UpdateAsync(entity, cancellationToken);
        }

        public Task UpdateAsync(VirtualNetwork entity, CancellationToken cancellationToken = default)
        {
            var networkConfig = _mapperConfig.CreateMapper().Map<VirtualNetworkConfigModel>(entity);
            return WriteProjectConfigAsync(networkConfig, cancellationToken);
        }

        public Task DeleteAsync(VirtualNetwork entity, CancellationToken cancellationToken = default)
        {
            File.Delete(Path.Combine(_configPath, $"{entity.Id}.json"));
            return Task.CompletedTask;
        }

        private async Task<VirtualNetworkConfigModel> ReadProjectConfigAsync(Guid networkId, CancellationToken cancellationToken)
        {
            var json = await File.ReadAllTextAsync(Path.Combine(_configPath, $"{networkId}.json"), cancellationToken);
            return JsonSerializer.Deserialize<VirtualNetworkConfigModel>(json);
        }

        private async Task WriteProjectConfigAsync(VirtualNetworkConfigModel configModel, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(configModel);
            await File.WriteAllTextAsync(Path.Combine(_configPath, $"{configModel.Id}.json"), json, Encoding.UTF8, cancellationToken);
        }
    }
}
