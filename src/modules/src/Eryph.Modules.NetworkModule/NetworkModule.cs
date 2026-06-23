using System;
using System.Collections.Generic;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Dbosoft.Rebus;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Components;
using Eryph.ModuleCore.Networks;
using Eryph.ModuleCore.Startup;
using Eryph.Rebus;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Subscriptions;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Network;

[UsedImplicitly]
public class NetworkModule
{
    private readonly bool _remoteEnabled;
    private readonly string _advertisedHost;

    public NetworkModule(IConfiguration configuration)
    {
        // Whether to advertise (and open) the OVN databases' remote SSL endpoints: done whenever the
        // process is enrolled for mTLS (the standalone network host), so any remote client — the
        // controller when it runs elsewhere, and the agents' ovn-controller — can reach them. In-process
        // (eryph-zero) mTLS is off, so nothing is advertised. This is independent of how the controller
        // itself dials the northbound DB: that local-pipe-vs-SSL choice is made per co-location in
        // OvnNorthboundConnectionProvider, not here.
        _remoteEnabled = bool.TryParse(configuration.GetSection("componentMtls")["enabled"], out var enabled)
                         && enabled;
        // The address remote clients dial. Defaults to the host FQDN identity (the same one used for
        // component registration and the mTLS certificate), which is resolvable across hosts/DNS domains
        // — unlike the short machine name — and matches the certificate the controller validates on
        // connect. An operator behind NAT/DNS can override it; a blank/whitespace override is treated as
        // unset (and trimmed) so it cannot advertise a malformed endpoint like "ssl:   :6641".
        _advertisedHost = configuration["ovn:advertisedHost"]?.Trim() is { Length: > 0 } host
            ? host
            : ComponentIdentity.GetLocalHostId();
    }

    [UsedImplicitly]
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.AddHostedService<OwnThreadOVSNodeHostedService<SyncedOVNDatabaseNode>>();
        options.AddHostedService<OwnThreadOVSNodeHostedService<NetworkControllerNode>>();
        options.AddStartupHandler<StartBusModuleHandler>();

        // Opt in to controller-driven component registration so the controller can track the network
        // process's liveness, route to its inbound queue, and resolve its OVN endpoints. The network
        // process consumes no distributed config domains, so it registers no realizers.
        // Suffix the inbound queue with the host FQDN identity (not the short machine name) so queue
        // names stay unique across DNS domains on a shared broker, matching the other components and
        // ComponentIdentity's own queue-naming convention.
        options.AddComponentRegistration(
            ComponentType.Network,
            $"{QueueNames.Network}.{ComponentIdentity.GetLocalHostId()}",
            BuildAdvertisedEndpoints());

        options.AddLogging();
    }

    // The OVN northbound/southbound SSL endpoints the controller and agents dial. Empty when the
    // databases are not exposed remotely (co-located/in-process), so nothing is advertised then.
    private Dictionary<string, string> BuildAdvertisedEndpoints()
    {
        if (!_remoteEnabled)
            return new Dictionary<string, string>();

        return new Dictionary<string, string>
        {
            [OvnRemoteEndpoints.NorthboundName] = $"ssl:{_advertisedHost}:{OvnRemoteEndpoints.NorthboundPort}",
            [OvnRemoteEndpoints.SouthboundName] = $"ssl:{_advertisedHost}:{OvnRemoteEndpoints.SouthboundPort}",
        };
    }

    [UsedImplicitly]
    public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
    {
        container.RegisterSingleton(serviceProvider.GetRequiredService<IAgentControlService>);
        container.RegisterSingleton<SyncedOVNDatabaseNode>();
        container.RegisterSingleton<NetworkControllerNode>();
        container.RegisterSingleton<IOVSService<SyncedOVNDatabaseNode>, OVSNodeService<SyncedOVNDatabaseNode>>();
        container.RegisterSingleton<IOVSService<NetworkControllerNode>, OVSNodeService<NetworkControllerNode>>();

        container.RegisterInstance(serviceProvider.GetRequiredService<WorkflowOptions>());
        container.Collection.Register(typeof(IHandleMessages<>), typeof(NetworkModule).Assembly);
        container.Collection.Append(typeof(IHandleMessages<>), typeof(FailedOperationTaskHandler<>), Lifestyle.Scoped);
        container.AddRebusOperationsHandlers();

        container.ConfigureRebus(configurer => configurer
            .Serialization(s => s.UseEryphSettings())
            // Use the registered component inbound queue as the single source of truth for the bus
            // endpoint name (it must match what AddComponentRegistration announced). Resolved inside
            // the transport lambda (bus start) so it does not trigger premature container verification
            // during ConfigureContainer.
            .Transport(t =>
                container.GetInstance<IRebusTransportConfigurer>()
                    .Configure(t, container.GetInstance<ComponentIdentity>().InboundQueue))
            .Options(x =>
            {
                x.RetryStrategy(secondLevelRetriesEnabled: true, errorDetailsHeaderMaxLength: 5);
                x.SetNumberOfWorkers(5);
                x.EnableSynchronousRequestReply();
            })
            .Subscriptions(s => container.GetService<IRebusConfigurer<ISubscriptionStorage>>()?.Configure(s))
            .Logging(x => x.MicrosoftExtensionsLogging(container.GetInstance<ILoggerFactory>()))
            .Start());
    }
}
