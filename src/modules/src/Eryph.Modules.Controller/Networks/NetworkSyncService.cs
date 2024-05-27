using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN.OSCommands.OVN;
using Dbosoft.OVN;
using Eryph.Core;
using Eryph.Core.Network;
using Eryph.Messages.Resources.Catlets.Events;
using Eryph.ModuleCore.Networks;
using Eryph.Rebus;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.Controller.Networks;

internal class NetworkSyncService : INetworkSyncService
{
    private readonly Container _container;
    private readonly INetworkProviderManager _providerManager;
    private readonly ILogger _log;

    public NetworkSyncService(
        Container container,
        INetworkProviderManager providerManager,
        ILogger log)
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

            var configRealizer = scope.GetInstance<INetworkProvidersConfigRealizer>();

            var stateStore = scope.GetInstance<IStateStore>();
            var providerManager = scope.GetInstance<INetworkProviderManager>();

            var bus = _container.GetInstance<IBus>();

            var config = await providerManager.GetCurrentConfiguration()
                .IfLeft(e => e.ToException().Rethrow<NetworkProvidersConfiguration>());

            await configRealizer.RealizeConfigAsync(config, cancellationToken);

            var projects = await stateStore.For<Project>().ListAsync(cancellationToken);
            
            var updatedNetworkNeighbors = new List<NetworkNeighborRecord>();
            foreach (var project in projects)
            {
                var result = await UpdateProjectNetworkPlan(project.Id);
                result.IfLeft(l =>
                {
                    _log.LogError(l.ToException(),
                        "Failed to apply network plan for project {ProjectName}({ProjectId})",
                        project.Name, project.Id);
                });
                result.IfRight(updatedNetworkNeighbors.AddRange);
            }

            await stateStore.SaveChangesAsync(cancellationToken);

            await bus.Advanced.Topics.Publish(
                $"broadcast_{QueueNames.VMHostAgent}",
                new NetworkNeighborsUpdateRequestedEvent
                {
                    UpdatedAddresses = updatedNetworkNeighbors.ToArray()
                });

            return Unit.Default;
        }

        return Prelude.TryAsync(RealizeProviderNetworksAsync).ToEither();
    }


    private EitherAsync<Error, NetworkNeighborRecord[]> UpdateProjectNetworkPlan(Guid projectId)
    {
        var sysEnv = _container.GetInstance<ISysEnvironment>();
        var ovnSettings = _container.GetInstance<IOVNSettings>();
        var planBuilder = _container.GetInstance<IProjectNetworkPlanBuilder>();
        var logger = _container.GetInstance<ILogger>();

        var networkPlanRealizer = new NetworkPlanRealizer(
            new OVNControlTool(sysEnv, ovnSettings.NorthDBConnection),
            logger);

        return from networkPlan in planBuilder.GenerateNetworkPlan(projectId, CancellationToken.None)
               from appliedNetworkPlan in networkPlanRealizer.ApplyNetworkPlan(networkPlan)
               let updatedNetworkNeighbors = appliedNetworkPlan.PlannedNATRules.Values
                   .Map(port => new NetworkNeighborRecord
                   {
                       IpAddress = port.ExternalIP,
                       MacAddress = port.ExternalMAC
                   }).ToArray()
               select updatedNetworkNeighbors;
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