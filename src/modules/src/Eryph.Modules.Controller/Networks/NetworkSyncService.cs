using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN.OSCommands.OVN;
using Dbosoft.OVN;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.ModuleCore.Networks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.Networks;

internal class NetworkSyncService : INetworkSyncService
{
    private readonly Container _container;
    private readonly INetworkProviderManager _providerManager;
    private readonly ILogger _log;
    public NetworkSyncService(Container container, INetworkProviderManager providerManager, ILogger log)
    {
        _container = container;
        _providerManager = providerManager;
        _log = log;
    }

    public EitherAsync<Error, Unit> SyncNetworks(CancellationToken cancellationToken)
    {
        async Task<Unit> SyncNetworksAsync(NetworkProvidersConfiguration providersConfiguration)
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(_container);
            var realizer = scope.GetInstance<INetworkConfigRealizer>();
            var stateStore = scope.GetInstance<IStateStore>();

            var projects = await stateStore.Read<Project>().ListAsync(new ProjectSpecs.GetAll(), cancellationToken);

            foreach (var project in projects)
            {
                try
                {
                    var networks = await stateStore.For<VirtualNetwork>()
                    .ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(project.Id), cancellationToken);

                    var projectConfig = networks.ToNetworksConfig(project.Name);
                    await realizer.UpdateNetwork(project.Id, projectConfig, providersConfiguration);
                    await stateStore.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to save network changes for project {projectId} ({projectName})", project.Id, project.Name);
                }

            }

            return Unit.Default;

        }

        return from providerConfig in _providerManager.GetCurrentConfiguration()
            from sync in Prelude.TryAsync(() => SyncNetworksAsync(providerConfig)).ToEither()
            from realize in RealizeProviderNetworks(cancellationToken)
            select Unit.Default;
    }

    public EitherAsync<Error, Unit> RealizeProviderNetworks(CancellationToken cancellationToken)
    {
        async Task<Unit> RealizeProviderNetworksAsync()
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(_container);

            var stateStore = scope.GetInstance<IStateStore>();
            var providerManager = scope.GetInstance<INetworkProviderManager>();

            var config =
                (await providerManager.GetCurrentConfiguration().ToEither()).IfLeft(
                    new NetworkProvidersConfiguration());

            var existingSubnets = await stateStore.For<ProviderSubnet>()
                .ListAsync(new NetplanBuilderSpecs.GetAllProviderSubnets(), cancellationToken);

            var existingIpPools = existingSubnets.SelectMany(x => x.IpPools).ToArray();
            var foundSubnets = new List<ProviderSubnet>();
            var foundIpPools = new List<IpPool>();

            foreach (var networkProvider in config.NetworkProviders.Where(x =>
                         x.Type is NetworkProviderType.NatOverLay or NetworkProviderType.Overlay))
            {
                foreach (var subnet in networkProvider.Subnets)
                {
                    var subnetEntity = existingSubnets.FirstOrDefault(e =>
                        e.ProviderName == networkProvider.Name && e.Name == subnet.Name);

                    if (subnetEntity != null)
                    {
                        foundSubnets.Add(subnetEntity);
                        subnetEntity.IpNetwork = subnet.Network;
                    }
                    else
                    {
                        subnetEntity = new ProviderSubnet
                        {
                            Id = Guid.NewGuid(),
                            IpNetwork = subnet.Network,
                            Name = subnet.Name,
                            ProviderName = networkProvider.Name,
                            IpPools = new List<IpPool>()
                        };

                        await stateStore.For<ProviderSubnet>().AddAsync(subnetEntity, cancellationToken);
                    }


                    foreach (var ipPool in subnet.IpPools)
                    {
                        var ipPoolEntity = subnetEntity.IpPools.FirstOrDefault(x =>
                            x.Name == ipPool.Name && x.FirstIp == ipPool.FirstIp && x.LastIp == ipPool.LastIp &&
                            x.IpNetwork == subnetEntity.IpNetwork);

                        if (ipPoolEntity != null)
                            foundIpPools.Add(ipPoolEntity);
                        else
                        {
                            await stateStore.For<IpPool>().AddAsync(new IpPool
                            {
                                Id = Guid.NewGuid(),
                                FirstIp = ipPool.FirstIp,
                                LastIp = ipPool.LastIp,
                                Counter = 0,
                                IpNetwork = subnet.Network,
                                Name = ipPool.Name,
                                SubnetId = subnetEntity.Id
                            }, cancellationToken);
                        }

                    }

                }

            }

            var removePools = existingIpPools.Where(e => foundIpPools.All(x => x.Id != e.Id)).ToArray();
            var removeSubnets = existingSubnets.Where(e => foundSubnets.All(x => x.Id != e.Id)).ToArray();

            await stateStore.For<IpPool>().DeleteRangeAsync(removePools, cancellationToken);
            await stateStore.For<ProviderSubnet>().DeleteRangeAsync(removeSubnets, cancellationToken);
            await stateStore.For<ProviderSubnet>().SaveChangesAsync(cancellationToken);


            var projects = (await stateStore.For<Project>().ListAsync(cancellationToken)).First();
            var res = await UpdateProjectNetworkPlan(projects.Id);
            await stateStore.For<ProviderSubnet>().SaveChangesAsync(cancellationToken);

            res.IfLeft(l =>
            {
                _log.LogError(
                    "Failed to apply network plans: {message}", l.Message);
                _log.LogDebug("Failed to apply network plans: {error}", l);
            });

            return Unit.Default;
        }

        return Prelude.TryAsync(RealizeProviderNetworksAsync).ToEither();
    }


    private EitherAsync<Error, Unit> UpdateProjectNetworkPlan(Guid projectId)
    {
        var sysEnv = _container.GetInstance<ISysEnvironment>();
        var ovnSettings = _container.GetInstance<IOVNSettings>();
        var planBuilder = _container.GetInstance<IProjectNetworkPlanBuilder>();
        var logger = _container.GetInstance<ILogger>();

        var networkplanRealizer =
            new NetworkPlanRealizer(new OVNControlTool(sysEnv, ovnSettings.NorthDBConnection), logger);


        return from networkPlan in planBuilder.GenerateNetworkPlan(projectId, CancellationToken.None)
               from _ in networkplanRealizer.ApplyNetworkPlan(networkPlan)
               select Unit.Default;
    }



    public EitherAsync<Error, string[]> ValidateChanges(NetworkProvider[] networkProviders)
    {
        async Task<string[]> ValidateChangesAsync()
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(_container);
            var validationService = scope.GetInstance<INetworkConfigValidator>();
            var stateStore = scope.GetInstance<IStateStore>();

            var projects = await stateStore.Read<Project>().ListAsync(new ProjectSpecs.GetAll());

            var changeMessages = new List<string>();


            foreach (var project in projects)
            {
                var tenantMsg = project.TenantId != EryphConstants.DefaultTenantId
                    ? $"tenant '{project.TenantId}', "
                    : "";

                var networks = await stateStore.For<VirtualNetwork>()
                    .ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(project.Id));

                var projectConfig = networks.ToNetworksConfig(project.Name);
                projectConfig = validationService.NormalizeConfig(projectConfig);

                var messages = validationService.ValidateConfig(projectConfig, networkProviders)
                    .Select(msg => $"{tenantMsg}project '{project.Name}' - {msg}").ToArray();

                changeMessages.AddRange(messages);

                if (messages.Length != 0) continue;
                await foreach (var message in validationService.ValidateChanges(project.Id, projectConfig,
                                   networkProviders))
                {
                    changeMessages.Add($"{tenantMsg}project '{project.Name}': '{message}'");
                }

            }

            return changeMessages.ToArray();

        }

        return Prelude.TryAsync(ValidateChangesAsync).ToEither();
    }
}