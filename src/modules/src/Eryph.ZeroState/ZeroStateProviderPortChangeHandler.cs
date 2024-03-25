using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.Configuration.Model;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.ZeroState
{
    internal class ZeroStateProviderPortChangeHandler : IZeroStateChangeHandler<ProviderPortChange>
    {
        private readonly IZeroStateConfig _config;
        private readonly IFileSystem _fileSystem;
        private readonly IStateStore _stateStore;

        public ZeroStateProviderPortChangeHandler(
            IZeroStateConfig config,
            IFileSystem fileSystem,
            IStateStore stateStore)
        {
            _config = config;
            _fileSystem = fileSystem;
            _stateStore = stateStore;
        }

        public async Task HandleChangeAsync(
            ProviderPortChange change,
            CancellationToken cancellationToken = default)
        {
            var floatingPorts = await _stateStore.For<FloatingNetworkPort>()
                .ListAsync(new FloatingNetworkPortSpecs.GetForConfig(), cancellationToken);

            var floatingPortsConfig = floatingPorts
                .Select(p => new FloatingNetworkPortConfigModel()
                {
                    Name = p.Name,
                    ProviderName = p.ProviderName,
                    SubnetName = p.SubnetName,
                    IpAssignments = p.IpAssignments.Select(a =>
                    {
                        var poolAssignment = a as IpPoolAssignment;
                        return new IpAssignmentConfigModel()
                        {
                            IpAddress = a.IpAddress,
                            PoolName = poolAssignment?.Pool.Name,
                            Number = poolAssignment?.Number,
                        };
                    }).ToArray()
                }).ToArray();

            var wrapper = new NetworkPortsConfigModel()
            {
                FloatingPorts = floatingPortsConfig,
            };
            var json = JsonSerializer.Serialize(wrapper);
            var path = Path.Combine(_config.NetworkPortsConfigPath, "ports.json");

            await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
        }
    }
}
