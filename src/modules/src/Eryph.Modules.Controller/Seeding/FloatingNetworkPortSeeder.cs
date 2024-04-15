using Eryph.Configuration.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Controller;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.StateDb;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Controller.Seeding;

internal class FloatingNetworkPortSeeder : IConfigSeeder<ControllerModule>
{
    private readonly IStateStore _stateStore;
    private readonly IFileSystem _fileSystem;
    private readonly string _configPath;

    public FloatingNetworkPortSeeder(
        ChangeTrackingConfig config,
        IStateStore stateStore,
        IFileSystem fileSystem)
    {
        _stateStore = stateStore;
        _fileSystem = fileSystem;
        _configPath = Path.Combine(config.NetworksConfigPath, "ports.json");
    }

    public async Task Execute(CancellationToken stoppingToken)
    {
        if (!_fileSystem.File.Exists(_configPath))
            return;

        _fileSystem.File.Copy(_configPath, $"{_configPath}.bak", true);

        var json = await _fileSystem.File.ReadAllTextAsync(_configPath, Encoding.UTF8, stoppingToken);
        var config = JsonSerializer.Deserialize<FloatingNetworkPortsConfigModel>(json);
        if (config is null)
            throw new SeederException($"The port configuration for network providers is invalid");

        foreach (var portConfig in config.FloatingPorts)
        {
            var existingPort = await _stateStore.For<FloatingNetworkPort>()
                .GetBySpecAsync(new FloatingNetworkPortSpecs.GetByName(
                        portConfig.ProviderName, portConfig.SubnetName, portConfig.Name),
                    stoppingToken);
            if (existingPort is not null)
                continue;

            var assignments = await portConfig.IpAssignments
                .Map(ac => ResolveIpAssignment(portConfig.ProviderName, ac, stoppingToken))
                .SequenceSerial();

            var port = new FloatingNetworkPort()
            {
                Name = portConfig.Name,
                MacAddress = portConfig.MacAddress,
                ProviderName = portConfig.ProviderName,
                SubnetName = portConfig.SubnetName,
                PoolName = portConfig.PoolName,
                IpAssignments = assignments.ToList(),
            };

            await _stateStore.For<NetworkPort>().AddAsync(port, stoppingToken);
        }

        await _stateStore.SaveChangesAsync(stoppingToken);
    }

    private async Task<IpAssignment?> ResolveIpAssignment(
        string providerName,
        IpAssignmentConfigModel config,
        CancellationToken stoppingToken)
    {
        var subnet = await _stateStore.For<ProviderSubnet>().GetBySpecAsync(
            new SubnetSpecs.GetByProviderName(providerName, config.SubnetName),
            stoppingToken);
        if (subnet is null)
            throw new SeederException(
                $"Cannot seed IP assignment {config.IpAddress} because subnet {config.SubnetName} does not exist for provider {providerName}");

        IpAssignment assignment;
        if (config.PoolName is not null)
        {
            await _stateStore.LoadCollectionAsync(subnet, s => s.IpPools, stoppingToken);
            var pool = subnet.IpPools.FirstOrDefault(p => p.Name == config.PoolName);
            if (pool is null)
                throw new SeederException(
                    $"Cannot seed IP assignment {config.IpAddress} because IP pool {config.PoolName} does not exist in subnet {config.SubnetName} of provider {providerName}");

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
