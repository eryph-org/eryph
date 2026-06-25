using System;
using Dbosoft.OVN;
using Dbosoft.OVN.Nodes;
using Dbosoft.Rebus;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Components;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Components;
using Eryph.ModuleCore.Networks;
using Eryph.ModuleCore.Startup;
using Eryph.Rebus;
using JetBrains.Annotations;
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
    [UsedImplicitly]
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        options.AddHostedService<OwnThreadOVSNodeHostedService<SyncedOVNDatabaseNode>>();
        options.AddHostedService<OwnThreadOVSNodeHostedService<NetworkControllerNode>>();
        options.AddStartupHandler<StartBusModuleHandler>();

        // Opt in to controller-driven component registration so the controller can track the network
        // process's liveness, route to its inbound queue, and resolve its OVN endpoints. The network
        // process consumes no distributed config domains, so it registers no realizers. It advertises no
        // endpoints itself — whether the OVN databases are exposed remotely (SSL) is the host's decision,
        // contributed through an IComponentEndpointProvider the standalone network host wires (eryph-zero
        // wires none, so in-process advertises nothing). Suffix the inbound queue with the host FQDN
        // identity so queue names stay unique across DNS domains on a shared broker.
        options.AddComponentRegistration(
            ComponentType.Network,
            $"{QueueNames.Network}.{ComponentIdentity.GetLocalHostId()}",
            null);

        options.AddLogging();
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
                x.EnableOperationCancellation(
                    container.GetInstance<WorkflowOptions>(),
                    container.GetInstance<ITaskCancellationRegistry>());
            })
            .Subscriptions(s => container.GetService<IRebusConfigurer<ISubscriptionStorage>>()?.Configure(s))
            .Logging(x => x.MicrosoftExtensionsLogging(container.GetInstance<ILoggerFactory>()))
            .Start());
    }
}
