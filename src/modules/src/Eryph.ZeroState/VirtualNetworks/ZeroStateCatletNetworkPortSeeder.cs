using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.ZeroState.VirtualNetworks;

internal class ZeroStateCatletNetworkPortSeeder : ZeroStateSeederBase
{
    private readonly IStateStore _stateStore;
    private readonly ILogger _logger;

    public ZeroStateCatletNetworkPortSeeder(
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
        var config = JsonSerializer.Deserialize<CatletNetworkPortsConfigModel>(json);
        if (config is null)
        {
            _logger.LogWarning("Could not seed project network ports for project {ProjectId} because the config is invalid",
                entityId);
            return;
        }

        foreach (var portConfig in config.CatletNetworkPorts)
        {
            await SeedPort(entityId, portConfig, cancellationToken);
        }

        await _stateStore.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPort(
        Guid projectId, 
        CatletNetworkPortConfigModel portConfig,
        CancellationToken cancellationToken)
    {
        var network = await _stateStore.For<VirtualNetwork>().GetBySpecAsync(
            new VirtualNetworkSpecs.GetByName(projectId, portConfig.VirtualNetworkName, portConfig.EnvironmentName),
            cancellationToken);
        if (network is null)
        {
            _logger.LogWarning("Could not seed project network port {PortName} because network {NetworkName} is missing",
                portConfig.VirtualNetworkName, portConfig.VirtualNetworkName);
            return;
        }

        bool exists = await _stateStore.For<CatletNetworkPort>().AnyAsync(
            new CatletNetworkPortSpecs.GetByName(network.Id, portConfig.Name),
            cancellationToken);
        if (exists)
            return;

        await _stateStore.LoadCollectionAsync(network, n => n.Subnets, cancellationToken);

        FloatingNetworkPort? floatingPort = null;
        if (portConfig.FloatingNetworkNetworkPort is not null)
        {
            floatingPort = await _stateStore.For<FloatingNetworkPort>().GetBySpecAsync(
                new FloatingNetworkPortSpecs.GetByName(
                    portConfig.FloatingNetworkNetworkPort.ProviderName,
                    portConfig.FloatingNetworkNetworkPort.SubnetName,
                    portConfig.FloatingNetworkNetworkPort.Name),
                cancellationToken);

            if (floatingPort is null)
            {
                _logger.LogWarning("Could not seed catlet network port {PortName} because floating port {FloatingPortName} is missing",
                    portConfig.VirtualNetworkName, portConfig.FloatingNetworkNetworkPort.Name);
                return;
            }
        }

        var assignments = await portConfig.IpAssignments
            .Map(ac => ResolveIpAssignment(network.Subnets, ac, cancellationToken))
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

    private async Task<IpAssignment?> ResolveIpAssignment(
        List<VirtualNetworkSubnet> subnets,
        IpAssignmentConfigModel config,
        CancellationToken stoppingToken)
    {
        var subnet = subnets.FirstOrDefault(s => s.Name == config.SubnetName);
        if (subnet is null)
        {
            _logger.LogWarning("Could not seed network port because subnet {SubnetName} is missing",
                config.SubnetName);
            return null;
        }

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

            var startIp = IPAddress.Parse(pool.FirstIp);
            var assignedIp = IPAddress.Parse(config.IpAddress);

            assignment = new IpPoolAssignment
            {
                Pool = pool,
                Number = (int)(IPNetwork.ToBigInteger(assignedIp) - IPNetwork.ToBigInteger(startIp)),
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
