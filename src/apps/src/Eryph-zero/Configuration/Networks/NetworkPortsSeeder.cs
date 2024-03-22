using Eryph.Configuration;
using Eryph.Modules.Controller;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

namespace Eryph.Runtime.Zero.Configuration.Networks
{
    internal class NetworkPortsSeeder : IConfigSeeder<ControllerModule>
    {
        private readonly ILogger _logger;
        private readonly IStateStore _stateStore;
        private readonly string _configPath;

        public NetworkPortsSeeder(
            ILogger logger,
            IStateStore stateStore)
        {
            _logger = logger;
            _stateStore = stateStore;
            _configPath = Path.Combine(ZeroConfig.GetNetworkPortsConfigPath(), "ports.json");
        }

        public async Task Execute(CancellationToken stoppingToken)
        {
            if(!File.Exists(_configPath))
                return;
            
            File.Copy(_configPath, _configPath + ".bak", true);

            var json = await File.ReadAllTextAsync(_configPath, stoppingToken);
            var configs = JsonSerializer.Deserialize<NetworkPortsConfigModel>(json);

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
                    Pool   = pool,
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
