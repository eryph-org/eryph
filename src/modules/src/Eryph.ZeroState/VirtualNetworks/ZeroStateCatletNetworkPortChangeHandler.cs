using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using Eryph.Configuration.Model;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.ZeroState.VirtualNetworks;

internal class ZeroStateCatletNetworkPortChangeHandler : IZeroStateChangeHandler<ZeroStateCatletNetworkPortChange>
{
    private readonly IZeroStateConfig _config;
    private readonly IFileSystem _fileSystem;
    private readonly IStateStore _stateStore;

    public ZeroStateCatletNetworkPortChangeHandler(
        IZeroStateConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore)
    {
        _config = config;
        _fileSystem = fileSystem;
        _stateStore = stateStore;
    }

    public async Task HandleChangeAsync(
        ZeroStateCatletNetworkPortChange change,
        CancellationToken cancellationToken = default)
    {
        foreach (var projectId in change.ProjectIds)
        {
            await HandleChangeAsync(projectId, cancellationToken);
        }
    }

    private async Task HandleChangeAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var ports = await _stateStore.For<CatletNetworkPort>().ListAsync(
            new CatletNetworkPortSpecs.GetForProjectConfig(projectId),
            cancellationToken);

        var config = new CatletNetworkPortsConfigModel()
        {
            CatletNetworkPorts = ports.Map(p => new CatletNetworkPortConfigModel()
            {
                Name = p.Name,
                VirtualNetworkName = p.Network.Name,
                EnvironmentName = p.Network.Environment,
                CatletMetadataId = p.CatletMetadataId,
                MacAddress = p.MacAddress,
                FloatingNetworkPort = p.FloatingPort is null
                    ? null
                    : new FloatingNetworkPortReferenceConfigModel()
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
                                SubnetName = pa.Subnet.Name,
                                PoolName = pa.Pool.Name,
                            }
                            : new IpAssignmentConfigModel()
                            {
                                IpAddress = a.IpAddress,
                                SubnetName = a.Subnet.Name,
                            })
                    .ToArray(),
            }).ToArray(),
        };

        var json = JsonSerializer.Serialize(config);
        var path = Path.Combine(_config.ProjectNetworkPortsConfigPath, $"{projectId}.json");
        await _fileSystem.File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken);
    }
}
