using Eryph.Configuration.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Controller;
using Eryph.StateDb;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.ZeroState.NetworkProviders;

internal class ZeroStateFloatingNetworkPortSeeder : IConfigSeeder<ControllerModule>
{
    private readonly IStateStore _stateStore;
    private readonly IFileSystem _fileSystem;
    private readonly string _configPath;
    private readonly ILogger<ZeroStateFloatingNetworkPortSeeder> _logger;

    public ZeroStateFloatingNetworkPortSeeder(
        IStateStore stateStore,
        IZeroStateConfig config,
        IFileSystem fileSystem,
        ILogger<ZeroStateFloatingNetworkPortSeeder> logger)
    {
        _stateStore = stateStore;
        _fileSystem = fileSystem;
        _configPath = Path.Combine(config.NetworksConfigPath, "ports.json");
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken stoppingToken = default)
    {
        if (!File.Exists(_configPath))
            return;

        File.Copy(_configPath, _configPath + ".bak", true);

        var json = await _fileSystem.File.ReadAllTextAsync(_configPath, Encoding.UTF8, stoppingToken);
        var configs = JsonSerializer.Deserialize<FloatingNetworkPortsConfigModel>(json);
        if (configs is null)
        {
            _logger.LogWarning("Could not deserialize network ports config from {ConfigPath}", _configPath);
            return;
        }

        foreach (var portConfig in configs.FloatingPorts)
        {
            var existingPort = await _stateStore.For<FloatingNetworkPort>()
                .GetBySpecAsync(new FloatingNetworkPortSpecs.GetByName(
                        portConfig.ProviderName, portConfig.SubnetName, portConfig.Name),
                    stoppingToken);
            if (existingPort is not null)
                continue;

            var subnet = await _stateStore.For<ProviderSubnet>().GetBySpecAsync(
                new SubnetSpecs.GetByProviderName(portConfig.ProviderName, portConfig.SubnetName),
                stoppingToken);
            if (subnet is null)
            {
                _logger.LogWarning("Could not seed network port {PortName} because subnet {SubnetName} is missing on provider {ProviderName}",
                    portConfig.Name, portConfig.SubnetName, portConfig.ProviderName);
                continue;
            }

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
        {
            _logger.LogWarning("Could not seed network port because subnet {SubnetName} is missing on provider {ProviderName}",
                config.SubnetName, providerName);
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

    public Task Execute(CancellationToken stoppingToken)
    {
        return SeedAsync(stoppingToken);
    }
}