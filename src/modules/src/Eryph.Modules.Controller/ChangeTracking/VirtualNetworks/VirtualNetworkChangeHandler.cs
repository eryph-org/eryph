using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.Configuration.Model;
using Eryph.ModuleCore.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;

namespace Eryph.Modules.Controller.ChangeTracking.VirtualNetworks;

internal class VirtualNetworkChangeHandler : IChangeHandler<VirtualNetworkChange>
{
    private readonly ChangeTrackingConfig _config;
    private readonly IFileSystem _fileSystem;
    private readonly IStateStore _stateStore;

    public VirtualNetworkChangeHandler(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore)
    {
        _config = config;
        _fileSystem = fileSystem;
        _stateStore = stateStore;
    }

    public async Task HandleChangeAsync(
        VirtualNetworkChange change,
        CancellationToken cancellationToken = default)
    {
        var projectId = change.ProjectId;

        var networksConfigPath = Path.Combine(_config.ProjectNetworksConfigPath, $"{projectId}.json");
        var portsConfigPath = Path.Combine(_config.ProjectNetworkPortsConfigPath, $"{projectId}.json");
        var project = await _stateStore.Read<Project>().GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            _fileSystem.File.Delete(networksConfigPath);
            _fileSystem.File.Delete(portsConfigPath);
            return;
        }

        var networks = await _stateStore.For<VirtualNetwork>().ListAsync(
            new VirtualNetworkSpecs.GetForChangeTracking(project.Id),
            cancellationToken);

        var networksConfig = PrepareNetworksConfig(project, networks);
        var portsConfig = PreparePortsConfig(networks);

        await _fileSystem.File.WriteAllTextAsync(
            networksConfigPath, networksConfig, Encoding.UTF8, cancellationToken);
        await _fileSystem.File.WriteAllTextAsync(
            portsConfigPath, portsConfig, Encoding.UTF8, cancellationToken);
    }

    private string PrepareNetworksConfig(Project project, List<VirtualNetwork> networks)
    {
        var networkConfig = networks.ToNetworksConfig(project.Name);
        return ConfigModelJsonSerializer.Serialize(networkConfig);
    }

    private string PreparePortsConfig(List<VirtualNetwork> networks)
    {
        var ports = networks.SelectMany(n => n.NetworkPorts).OfType<CatletNetworkPort>();
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

        return JsonSerializer.Serialize(config);
    }
}
