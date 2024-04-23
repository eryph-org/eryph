using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration.Model;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;

namespace Eryph.Modules.Controller.Seeding;

internal class CatletNetworkPortSeeder : SeederBase
{
    private readonly IStateStore _stateStore;

    public CatletNetworkPortSeeder(
        ChangeTrackingConfig config,
        IFileSystem fileSystem,
        IStateStore stateStore)
        : base(fileSystem, config.ProjectNetworkPortsConfigPath)
    {
        _stateStore = stateStore;
    }

    protected override async Task SeedAsync(
        Guid entityId,
        string json,
        CancellationToken cancellationToken = default)
    {
        var config = JsonSerializer.Deserialize<CatletNetworkPortsConfigModel>(json);
        if (config is null)
            throw new SeederException($"The network port configuration for project {entityId} is invalid");

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
            throw new SeederException(
                $"Cannot seed port {portConfig.Name} because network {portConfig.VirtualNetworkName} does not exist in environment {portConfig.EnvironmentName}");

        var exists = await _stateStore.For<CatletNetworkPort>().AnyAsync(
            new CatletNetworkPortSpecs.GetByName(network.Id, portConfig.Name),
            cancellationToken);
        if (exists)
            return;

        await _stateStore.LoadCollectionAsync(network, n => n.Subnets, cancellationToken);

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
                throw new SeederException(
                    $"Cannot seed port {portConfig.Name} because floating port {portConfig.FloatingNetworkPort.Name} does not exist");
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
            throw new SeederException(
                $"Cannot seed IP assignment {config.IpAddress} because subnet {config.SubnetName} does not exist");

        IpAssignment assignment;
        if (config.PoolName is not null)
        {
            await _stateStore.LoadCollectionAsync(subnet, s => s.IpPools, stoppingToken);
            var pool = subnet.IpPools.FirstOrDefault(p => p.Name == config.PoolName);
            if (pool is null)
                throw new SeederException(
                    $"Cannot seed IP assignment {config.IpAddress} because IP pool {config.PoolName} does not exist in subnet {config.SubnetName}");

            var startIp = IPAddress.Parse(pool.FirstIp);
            var assignedIp = IPAddress.Parse(config.IpAddress);

            assignment = new IpPoolAssignment
            {
                Pool = pool,
                Number = (int)(IPNetwork2.ToBigInteger(assignedIp) - IPNetwork2.ToBigInteger(startIp)),
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
