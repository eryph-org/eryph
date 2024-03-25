
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.Configuration.Model;
using Eryph.ModuleCore.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.ZeroState
{
    internal class ZeroStateVirtualNetworkPortChangeHandler : IZeroStateChangeHandler<VirtualNetworkPortChange>
    {
        private readonly IZeroStateConfig _config;
        private readonly IFileSystem _fileSystem;
        private readonly IStateStore _stateStore;

        public ZeroStateVirtualNetworkPortChangeHandler(
            IZeroStateConfig config,
            IFileSystem fileSystem,
            IStateStore stateStore)
        {
            _config = config;
            _fileSystem = fileSystem;
            _stateStore = stateStore;
        }

        public async Task HandleChangeAsync(
            VirtualNetworkPortChange change,
            CancellationToken cancellationToken = default)
        {
            foreach (var projectId in change.ProjectIds)
            {
                var ports = await _stateStore.For<CatletNetworkPort>()
                    .ListAsync(new CatletNetworkPortSpecs.GetForProjectConfig(projectId), cancellationToken);

                var config = new ProjectNetworkPortsConfig()
                {
                    CatletNetworkPorts = ports.Map(p => new CatletNetworkPortConfigModel()
                    {
                        VirtualNetworkName = p.Network.Name,
                        // TODO improve me
                        CatletMetadataId = p.Catlet!.MetadataId,
                        MacAddress = p.MacAddress,
                        FloatingNetworkPort = p.FloatingPort is null
                            ? null
                            : new FloatingPortReferenceConfigModel()
                            {
                                Name = p.FloatingPort.Name,
                                SubnetName = p.FloatingPort.SubnetName,
                                ProviderName = p.FloatingPort.ProviderName,

                            },
                        IpAssignments = p.IpAssignments?.Map(a =>
                                a is IpPoolAssignment pa
                                    ? new IpAssignmentConfigModel()
                                    {
                                        IpAddress = pa.IpAddress,
                                        PoolName = pa.Pool.Name,
                                        Number = pa.Number,
                                    }
                                    : new IpAssignmentConfigModel()
                                    {
                                        IpAddress = a.IpAddress
                                    })
                            .ToArray(),
                    }).ToArray(),
                };

                var json = JsonSerializer.Serialize(config);
                var path = Path.Combine(_config.ProjectNetworksConfigPath, $"{projectId}.json");
                await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
            }
        }
    }
}
