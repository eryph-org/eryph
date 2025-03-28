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

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Networks;

internal class NetworkSyncService(
    Container container,
    INetworkProviderManager providerManager)
    : INetworkSyncService
{
    public EitherAsync<Error, Unit> SyncNetworks(CancellationToken cancellationToken) =>
        from providerConfig in providerManager.GetCurrentConfiguration()
        from _ in SyncNetworks(providerConfig)
            .Run().Map(fin => fin.ToEither()).AsTask().ToAsync()
        select Unit.Default;

    private Aff<Unit> SyncNetworks(
        NetworkProvidersConfiguration providersConfiguration) =>
        from _1 in RealizeProviderNetworks(providersConfiguration)
        from _2 in RealizeProjectNetworks(providersConfiguration)
        from applyResult in ApplyNetworkPlans(providersConfiguration)
        from updateResult in UpdateNetworkNeighbors(applyResult.Neighbors)
            .Map(_ => Option<Error>.None)
            .IfFail(e => Error.New("Failed to update network neighbors.", e))
        // The application of the networks plans and the update of the network
        // neighbors is best-effort. We try to do as much as possible before
        // raising an error to ensure that as much as possible of the networking
        // keeps working.
        from _ in applyResult.Error.Append(updateResult.ToSeq()).Match(
            Empty: () => unitAff,
            Seq: errors => FailAff<Unit>(
                Error.New("Failed to apply network plans for projects.", Error.Many(errors))))
        select unit;

    private Aff<Unit> RealizeProviderNetworks(
        NetworkProvidersConfiguration providerConfig) =>
        use(Eff(() => AsyncScopedLifestyle.BeginScope(container)),
            scope =>
                from _ in unitAff
                let configRealizer = scope.GetInstance<INetworkProvidersConfigRealizer>()
                from __ in Aff(async () => await configRealizer.RealizeConfigAsync(providerConfig, default).ToUnit())
                select unit);

    private Aff<Unit> RealizeProjectNetworks(
        NetworkProvidersConfiguration providerConfig) =>
        from projects in use(
            Eff(() => AsyncScopedLifestyle.BeginScope(container)),
            scope =>
                from _ in unitAff
                let stateStore = scope.GetInstance<IStateStore>()
                from projects in stateStore.Read<Project>().IO.ListAsync().ToAff(e => e)
                select projects)
        from errors in projects
            .Map(p => RealizeProjectNetworks(p, providerConfig)
                .Map(_ => Option<Error>.None)
                .IfFail(e => Error.New($"Failed to save network changes for project {p.Name}({p.Id}).", e)))
            .SequenceSerial()
        from _ in errors.Somes().Match(
            Empty: () => unitAff,
            Seq: errors => FailAff<Unit>(Error.New("Failed to save network changes for projects.", Error.Many(errors))))
        select unit;

    private Aff<Unit> RealizeProjectNetworks(
        Project project,
        NetworkProvidersConfiguration providerConfig) =>
        use(Eff(() => AsyncScopedLifestyle.BeginScope(container)),
            scope =>
                from _1 in unitAff
                let stateStore = scope.GetInstance<IStateStore>()
                let ipManager = scope.GetInstance<IProviderIpManager>()
                from networks in stateStore.For<VirtualNetwork>().IO.ListAsync(
                        new VirtualNetworkSpecs.GetForNetworkSync(project.Id))
                    .ToAff(e => e)
                from config in Eff(() => networks.ToNetworksConfig(project.Name))
                let realizer = scope.GetInstance<INetworkConfigRealizer>()
                from _2 in Aff(async () => await realizer.UpdateNetwork(project.Id, config, providerConfig).ToUnit())
                // Configure any missing floating IPs. Some floating IPs might have been
                // removed when the network provider configuration was updated.
                from _3 in networks.SelectMany(n => n.NetworkPorts, (n, p) => (Provider: n.NetworkProvider, Port: p))
                    .Map(t => from fp in Optional(t.Port.FloatingPort)
                              select (t.Provider, Port:  fp))
                    .Somes()
                    .Map(t => ipManager.ConfigureFloatingPortIps(t.Provider, t.Port).ToAff(e => e))
                    .SequenceSerial()
                from _4 in Aff(async () => await stateStore.SaveChangesAsync().ToUnit())
                select unit);

    private Aff<(Seq<NetworkNeighborRecord> Neighbors, Seq<Error> Error)> ApplyNetworkPlans(
        NetworkProvidersConfiguration providerConfig) =>
        from projects in use(
            Eff(() => AsyncScopedLifestyle.BeginScope(container)),
            scope =>
                from _ in unitAff
                let stateStore = scope.GetInstance<IStateStore>()
                from projects in stateStore.Read<Project>().IO.ListAsync().ToAff(e => e)
                select projects)
        from results in projects
            .Map(p => ApplyNetworkPlan(p.Id, providerConfig)
                .Map(r => (Neighbors: r, Error: Option<Error>.None))
                .IfFail(e => (
                    Seq<NetworkNeighborRecord>(),
                    Error.New($"Failed to apply network plan for project {p.Name}({p.Id}).", e))))
            .SequenceSerial()
        let neighbors = results.Map(r => r.Neighbors).Flatten()
        let error = results.Map(r => r.Error).Somes()
        select (neighbors, error);

    private Aff<Seq<NetworkNeighborRecord>> ApplyNetworkPlan(
        Guid projectId,
        NetworkProvidersConfiguration providerConfig) =>
        use(Eff(() => AsyncScopedLifestyle.BeginScope(container)),
            scope =>
                from _ in unitAff
                let sysEnv = scope.GetInstance<ISystemEnvironment>()
                let ovnSettings = scope.GetInstance<IOVNSettings>()
                let planBuilder = scope.GetInstance<IProjectNetworkPlanBuilder>()
                let logger = scope.GetInstance<ILogger>()
                let networkPlanRealizer = new NetworkPlanRealizer(
                    new OVNControlTool(sysEnv, ovnSettings.NorthDBConnection),
                    logger)
                from networkPlan in planBuilder.GenerateNetworkPlan(projectId, providerConfig).ToAff(e => e)
                from appliedNetworkPlan in networkPlanRealizer.ApplyNetworkPlan(networkPlan).ToAff(e => e)
                let updatedNetworkNeighbors = appliedNetworkPlan.PlannedNATRules.Values
                    .Map(port => new NetworkNeighborRecord
                    {
                        IpAddress = port.ExternalIP,
                        MacAddress = port.ExternalMAC
                    }).ToSeq()
                select updatedNetworkNeighbors);

    private Aff<Unit> UpdateNetworkNeighbors(
        Seq<NetworkNeighborRecord> neighbors) =>
        use(Eff(() => AsyncScopedLifestyle.BeginScope(container)),
            scope =>
                from _ in unitAff
                let bus = scope.GetInstance<IBus>()
                from __ in Aff(async () => await bus.Advanced.Topics.Publish(
                        $"broadcast_{QueueNames.VMHostAgent}",
                        new NetworkNeighborsUpdateRequestedEvent
                        {
                            UpdatedAddresses = neighbors.ToArray()
                        })
                    .ToUnit())
                select unit);

    public EitherAsync<Error, string[]> ValidateChanges(NetworkProvider[] networkProviders)
    {
        async Task<string[]> ValidateChangesAsync()
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(container);
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

        return TryAsync(ValidateChangesAsync).ToEither();
    }
}