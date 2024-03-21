using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.ModuleCore.Networks;
using Eryph.Runtime.Zero.Configuration;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Runtime.Zero.ZeroState
{
    internal class ZeroStateBackgroundService : BackgroundService
    {
        private readonly Container _container;
        private readonly ILogger<ZeroStateBackgroundService> _logger;
        private readonly IZeroStateChannel<ZeroStateChangeSet> _channel;

        public ZeroStateBackgroundService(
            Container container,
            ILogger<ZeroStateBackgroundService> logger,
            IZeroStateChannel<ZeroStateChangeSet> channel)
        {
            _container = container;
            _logger = logger;
            _channel = channel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogError("Zero state background service is running...");
            while (!stoppingToken.IsCancellationRequested)
            {
                var changeSet = await _channel.ReadAsync(stoppingToken);
                try
                {
                    await ProcessChangeSet(changeSet, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process change set for transaction {TransactionId}", changeSet.TransactionId);
                }
            }
        }

        private async Task ProcessChangeSet(ZeroStateChangeSet changeSet, CancellationToken stoppingToken)
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
                var project =  await stateStore.Read<Project>().GetByIdAsync(projectId, stoppingToken);
                var networks = await stateStore.For<VirtualNetwork>()
                    .ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(project.Id), stoppingToken);

                var networkConfig = networks.ToNetworksConfig(project.Name);
                var json = ConfigModelJsonSerializer.Serialize(networkConfig);
                var path = Path.Combine(ZeroConfig.GetProjectsConfigPath(), "networks", $"{project.Id}.json");
                await File.WriteAllTextAsync(path, json, Encoding.UTF8, stoppingToken);
            }
        }
    }
}
