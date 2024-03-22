using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.Configuration.Model;
using Eryph.ModuleCore.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.ZeroState
{
    public class ZeroStateBackgroundService : BackgroundService
    {
            private readonly Container _container;
            private readonly ILogger<ZeroStateBackgroundService> _logger;
            private readonly ZeroStateQueue<VirtualNetworkChange> _queue;
            private readonly IZeroStateConfig _config;

            public ZeroStateBackgroundService(
                Container container,
                ILogger<ZeroStateBackgroundService> logger,
                //ZeroStateQueue queue,
                IZeroStateConfig config)
            {
                _container = container;
                _logger = logger;
                //_queue = queue;
                _config = config;
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                _logger.LogError("Zero state background service is running...");
                while (!stoppingToken.IsCancellationRequested)
                {
                    var changeSet = await _queue.DequeueAsync(stoppingToken);
                    try
                    {
                        //await ProcessChangeSet(changeSet, stoppingToken);
                        //await ProcessFloatingPortChanges(changeSet, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process change set for transaction {TransactionId}", changeSet.TransactionId);
                    }
                }
            }

            private async Task ProcessChangeSet(ZeroStateQueueItem changeSet, CancellationToken stoppingToken)
            {
                _logger.LogError("Received change set for transaction {TransactionId}", changeSet.TransactionId);
                await using var scope = AsyncScopedLifestyle.BeginScope(_container);

                var networkIds = changeSet.Changes
                    .Where(x => x.EntityType == typeof(VirtualNetwork))
                    .Select(i => i.Id)
                    .Distinct();
                var dbContext = scope.GetRequiredService<StateStoreContext>();
                var projectIds = await dbContext.VirtualNetworks
                    .Where(x => networkIds.Contains(x.Id))
                    .Select(x => x.ProjectId)
                    .Distinct()
                    .ToListAsync(stoppingToken);

                foreach (var projectId in projectIds)
                {
                    var stateStore = scope.GetRequiredService<IStateStore>();
                    var project = await stateStore.Read<Project>().GetByIdAsync(projectId, stoppingToken);
                    var networks = await stateStore.For<VirtualNetwork>()
                        .ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(project.Id), stoppingToken);

                    var networkConfig = networks.ToNetworksConfig(project.Name);
                    var json = ConfigModelJsonSerializer.Serialize(networkConfig);
                    var path = Path.Combine(_config.ProjectNetworksConfigPath, $"{project.Id}.json");
                    await File.WriteAllTextAsync(path, json, Encoding.UTF8, stoppingToken);
                }
            }

            private async Task ProcessFloatingPortChanges(ZeroStateQueueItem changeSet, CancellationToken stoppingToken)
            {
                _logger.LogError("Received change set for transaction {TransactionId}", changeSet.TransactionId);
                await using var scope = AsyncScopedLifestyle.BeginScope(_container);

                bool anyFloatingPortChanges = changeSet.Changes.Any(x => x.EntityType == typeof(FloatingNetworkPort));
                if (!anyFloatingPortChanges)
                {
                    //TODO check IP assignments of floating ports
                    return;
                }

                var stateStore = scope.GetRequiredService<IStateStore>();
                var floatingPorts = await stateStore.For<FloatingNetworkPort>()
                    .ListAsync(new FloatingNetworkPortSpecs.GetForConfig(), stoppingToken);

                var floatingPortsConfig = floatingPorts
                    .Select(p => new FloatingNetworkPortConfigModel()
                    {
                        Name = p.Name,
                        ProviderName = p.ProviderName,
                        SubnetName = p.SubnetName,
                        IpAssignments = p.IpAssignments.Select(a =>
                        {
                            var poolAssignment = a as IpPoolAssignment;
                            return new IpAssignmentConfigModel()
                            {
                                IpAddress = a.IpAddress,
                                PoolName = poolAssignment?.Pool.Name,
                                Number = poolAssignment?.Number,
                            };
                        }).ToArray()
                    }).ToArray();

                var wrapper = new NetworkPortsConfigModel()
                {
                    FloatingPorts = floatingPortsConfig,
                };
                var json = JsonSerializer.Serialize(wrapper);
                var path = Path.Combine(_config.NetworkPortsConfigPath, "ports.json");

                await File.WriteAllTextAsync(path, json, Encoding.UTF8, stoppingToken);
            }
        }
    }
