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

namespace Eryph.Modules.Controller.ChangeTracking.VirtualNetworks;

internal class VirtualNetworkChangeHandler(
    ChangeTrackingConfig config,
    IFileSystem fileSystem,
    IStateStore stateStore)
    : IChangeHandler<VirtualNetworkChange>
{
    public async Task HandleChangeAsync(
        VirtualNetworkChange change,
        CancellationToken cancellationToken = default)
    {
        var projectId = change.ProjectId;

        var networksConfigPath = Path.Combine(config.ProjectNetworksConfigPath, $"{projectId}.json");
        var portsConfigPath = Path.Combine(config.ProjectNetworkPortsConfigPath, $"{projectId}.json");
        var project = await stateStore.Read<Project>().GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            fileSystem.File.Delete(networksConfigPath);
            fileSystem.File.Delete(portsConfigPath);
            return;
        }

        var networks = await stateStore.For<VirtualNetwork>().ListAsync(
            new VirtualNetworkSpecs.GetForChangeTracking(project.Id),
            cancellationToken);

        var networksConfig = PrepareNetworksConfig(project, networks);
        var portsConfig = PreparePortsConfig(networks);

        await fileSystem.File.WriteAllTextAsync(
            networksConfigPath, networksConfig, Encoding.UTF8, cancellationToken);
        await fileSystem.File.WriteAllTextAsync(
            portsConfigPath, portsConfig, Encoding.UTF8, cancellationToken);
    }

    private static string PrepareNetworksConfig(Project project, List<VirtualNetwork> networks)
    {
        var networkConfig = networks.ToNetworksConfig(project.Name);
        return ProjectNetworksConfigJsonSerializer.Serialize(networkConfig);
    }

    private static string PreparePortsConfig(List<VirtualNetwork> networks)
    {
        var ports = networks.SelectMany(n => n.NetworkPorts).OfType<CatletNetworkPort>();
        var config = new CatletNetworkPortsConfigModel
        {
            CatletNetworkPorts = ports.Map(p => new CatletNetworkPortConfigModel
            {
                Name = p.Name,
                VirtualNetworkName = p.Network.Name,
                EnvironmentName = p.Network.Environment,
                CatletMetadataId = p.CatletMetadataId,
                AddressName = p.AddressName,
                MacAddress = p.MacAddress,
                FloatingNetworkPort = p.FloatingPort is null
                    ? null
                    : new FloatingNetworkPortReferenceConfigModel
                    {
                        Name = p.FloatingPort.Name,
                        SubnetName = p.FloatingPort.SubnetName,
                        ProviderName = p.FloatingPort.ProviderName,
                    },
                IpAssignments = p.IpAssignments?.Map(a =>
                        a is IpPoolAssignment pa
                            ? new IpAssignmentConfigModel
                            {
                                IpAddress = pa.IpAddress,
                                SubnetName = pa.Subnet!.Name,
                                PoolName = pa.Pool.Name,
                            }
                            : new IpAssignmentConfigModel
                            {
                                IpAddress = a.IpAddress,
                                SubnetName = a.Subnet!.Name,
                            })
                    .ToArray(),
            }).ToArray(),
        };

        return JsonSerializer.Serialize(config);
    }
}
