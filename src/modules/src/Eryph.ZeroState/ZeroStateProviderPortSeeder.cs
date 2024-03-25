using Eryph.Configuration.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.StateDb;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.ZeroState
{
    internal class ZeroStateProviderPortSeeder : IZeroStateSeeder
    {
        private readonly IStateStore _stateStore;
        private readonly IFileSystem _fileSystem;
        private readonly string _configPath;
        private readonly ILogger<ZeroStateProviderPortSeeder> _logger;

        public ZeroStateProviderPortSeeder(
            IStateStore stateStore,
            IZeroStateConfig config,
            IFileSystem fileSystem,
            ILogger<ZeroStateProviderPortSeeder> logger)
        {
            _stateStore = stateStore;
            _fileSystem = fileSystem;
            _configPath = Path.Combine(config.NetworkPortsConfigPath, "ports.json");
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken stoppingToken = default)
        {
            if (!File.Exists(_configPath))
                return;

            File.Copy(_configPath, _configPath + ".bak", true);

            var json = await _fileSystem.File.ReadAllTextAsync(_configPath, Encoding.UTF8, stoppingToken);
            var configs = JsonSerializer.Deserialize<NetworkPortsConfigModel>(json);
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
                    .Map(ac => ResolveIpAssignment(subnet, ac, stoppingToken))
                    .SequenceSerial();

                var port = new FloatingNetworkPort()
                {
                    ProviderName = portConfig.ProviderName,
                    SubnetName = portConfig.SubnetName,
                    Name = portConfig.Name,
                    PoolName = portConfig.PoolName,
                    IpAssignments = assignments.ToList(),
                };

                await _stateStore.For<NetworkPort>().AddAsync(port, stoppingToken);
            }

            await _stateStore.SaveChangesAsync(stoppingToken);
        }

        private async Task<IpAssignment?> ResolveIpAssignment(
            Subnet subnet,
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
}
