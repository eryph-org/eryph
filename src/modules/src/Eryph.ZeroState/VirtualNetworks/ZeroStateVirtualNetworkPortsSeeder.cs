using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.ZeroState.VirtualNetworks;

internal class ZeroStateVirtualNetworkPortsSeeder : ZeroStateSeederBase
{
    private readonly IStateStore _stateStore;
    private readonly ILogger _logger;

    public ZeroStateVirtualNetworkPortsSeeder(
        IFileSystem fileSystem,
        IZeroStateConfig config,
        IStateStore stateStore,
        ILogger logger)
        : base(fileSystem, config.ProjectNetworkPortsConfigPath)
    {
        _stateStore = stateStore;
        _logger = logger;
    }

    protected override async Task SeedAsync(
        Guid entityId,
        string json,
        CancellationToken cancellationToken = default)
    {
        var config = JsonSerializer.Deserialize<ProjectNetworkPortsConfig>(json);
        if (config is null)
        {
            _logger.LogWarning("Could not seed project network ports for project {ProjectId} because the config is invalid",
                entityId);
            return;
        }

        foreach (var portConfig in config.CatletNetworkPorts)
        {
            var network = await _stateStore.For<VirtualNetwork>().GetBySpecAsync(
                new VirtualNetworkSpecs.GetByName(entityId, portConfig.VirtualNetworkName, portConfig.EnvironmentName),
                cancellationToken);
            if (network is null)
            {
                _logger.LogWarning("Could not seed project network port {PortName} because network {NetworkName} is missing",
                    portConfig.VirtualNetworkName, portConfig.VirtualNetworkName);
                continue;
            }

            await _stateStore.LoadCollectionAsync(network, n => n.Subnets, cancellationToken);

            // TODO where to store the subnet name?
            string subnetName = "default";
            var subnet = network.Subnets.FirstOrDefault(s => s.Name == subnetName);
            if (subnet is null)
            {
                _logger.LogWarning("Could not seed network port {PortName} because subnet {SubnetName} is missing in network {NetworkName} of project {ProjectId}",
                    portConfig.Name, subnetName, network.Name, entityId);
                continue;
            }

            FloatingNetworkPort? floatingPort = null;
            if (portConfig.FloatingNetworkPort is not null)
            {
                floatingPort = await _stateStore.For<FloatingNetworkPort>().GetBySpecAsync(
                    new FloatingNetworkPortSpecs.GetByName(
                        portConfig.FloatingNetworkPort.ProviderName,
                        portConfig.FloatingNetworkPort.SubnetName,
                        portConfig.FloatingNetworkPort.Name),
                    cancellationToken);

                if (floatingPort is null)
                {
                    _logger.LogWarning("Could not seed project network port {PortName} because floating port {FloatingPortName} is missing",
                        portConfig.VirtualNetworkName, portConfig.FloatingNetworkPort.Name);
                    continue;
                }
            }

            var assignments = await portConfig.IpAssignments
                .Map(ac => ResolveIpAssignment(subnet, ac, cancellationToken))
                .SequenceSerial();

            var port = new CatletNetworkPort()
            {
                Name = portConfig.Name,
                MacAddress = portConfig.MacAddress,
                FloatingPort = floatingPort,
                Network = network,
                CatletMetadataId = portConfig.CatletMetadataId,
                IpAssignments = assignments.ToList(),
            };

            await _stateStore.For<CatletNetworkPort>().AddAsync(port, cancellationToken);
        }

        await _stateStore.SaveChangesAsync(cancellationToken);
    }

    private async Task<IpAssignment?> ResolveIpAssignment(
        VirtualNetworkSubnet subnet,
        IpAssignmentConfigModel config,
        CancellationToken stoppingToken)
    {
        IpAssignment assignment;
        if (config.PoolName is not null)
        {
            await _stateStore.LoadCollectionAsync(subnet, s => s.IpPools, stoppingToken);
            var pool = subnet.IpPools.FirstOrDefault(p => p.Name == config.PoolName);
            if (pool is null)
            {
                _logger.LogWarning("Could not seed ip assignment {IpAddress} because pool {PoolName} is missing on subnet {SubnetName}",
                    config.IpAddress, config.PoolName, subnet.Name);
                return null;
            }
            assignment = new IpPoolAssignment
            {
                Pool = pool,
                Number = config.Number.Value!,
                Subnet = subnet,
            };
        }
        else
        {
            assignment = new IpAssignment();
        }

        assignment.IpAddress = config.IpAddress;

        return assignment;
    }
}
