using System;
using System.IO.Abstractions;
using Dbosoft.Rebus;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Core;
using Eryph.DistributedLock;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Components;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.Components;
using Eryph.Modules.Controller.Inventory;
using Eryph.Modules.Controller.Networks;
using Eryph.Modules.Controller.Operations;
using Eryph.Modules.Controller.Seeding;
using Eryph.Rebus;
using Eryph.StateDb;
using Eryph.StateDb.Workflows;
using IdGen;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Sagas;
using Rebus.Sagas.Exclusive;
using Rebus.Subscriptions;
using Rebus.Timeouts;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using IFileSystem = System.IO.Abstractions.IFileSystem;

namespace Eryph.Modules.Controller;

[UsedImplicitly]
public class ControllerModule
{
    private readonly ChangeTrackingConfig _changeTrackingConfig = new();

    private readonly IConfiguration _configuration;
    private readonly InventoryConfig _inventoryConfig = new();
    private readonly OperationsHousekeepingConfig _operationsHousekeepingConfig = new();

    public ControllerModule(IConfiguration configuration)
    {
        _configuration = configuration;

        configuration.GetSection("ChangeTracking")
            .Bind(_changeTrackingConfig);

        configuration.GetSection("Inventory")
            .Bind(_inventoryConfig);

        configuration.GetSection("Housekeeping:Operations")
            .Bind(_operationsHousekeepingConfig);
    }

    public string Name => "Eryph.Controller";

    [UsedImplicitly]
    public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
    {
        services.AddTransient<InventoryTimerJob>();
        services.AddQuartz(q =>
        {
            q.SchedulerName = $"{Name}.Scheduler";

            q.AddJob<InventoryTimerJob>(job => job.WithIdentity(InventoryTimerJob.Key)
                .DisallowConcurrentExecution());
            q.AddJob<VirtualDiskCleanupJob>(job => job.WithIdentity(VirtualDiskCleanupJob.Key)
                .DisallowConcurrentExecution());
            q.AddJob<OperationCleanupJob>(job => job.WithIdentity(OperationCleanupJob.Key)
                .DisallowConcurrentExecution());

            q.AddTrigger(trigger => trigger.WithIdentity("InventoryTimerJobTrigger")
                .ForJob(InventoryTimerJob.Key)
                .StartNow()
                .WithSimpleSchedule(s => s.WithInterval(_inventoryConfig.InventoryInterval).RepeatForever()));
            q.AddTrigger(trigger => trigger.WithIdentity("VirtualDiskCleanupJobTrigger")
                .ForJob(VirtualDiskCleanupJob.Key)
                .StartNow()
                .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromHours(1)).RepeatForever()));
            q.AddTrigger(trigger => trigger.WithIdentity("OperationCleanupJobTrigger")
                .ForJob(OperationCleanupJob.Key)
                .StartNow()
                .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromHours(1)).RepeatForever()));

            // The scheduled trigger will only fire the first time after waiting for one interval.
            // We add another trigger without a schedule to trigger the job immediately when
            // the scheduler starts.
            q.AddTrigger(trigger => trigger.WithIdentity("InventoryTimerJobStartupTrigger")
                .ForJob(InventoryTimerJob.Key)
                .StartNow());
            q.AddTrigger(trigger => trigger.WithIdentity("VirtualDiskCleanupJobStartupTrigger")
                .ForJob(VirtualDiskCleanupJob.Key)
                .StartNow());
            q.AddTrigger(trigger => trigger.WithIdentity("OperationCleanupJobStartupTrigger")
                .ForJob(OperationCleanupJob.Key)
                .StartNow());
        });
        services.AddQuartzHostedService();
    }

    [UsedImplicitly]
    public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
    {
        container.RegisterSingleton<IFileSystem, FileSystem>();
        container.RegisterInstance(_changeTrackingConfig);
        container.RegisterInstance(_operationsHousekeepingConfig);

        container.Register<IRebusUnitOfWork, StateStoreDbUnitOfWork>(Lifestyle.Scoped);
        container.Collection.Register(typeof(IHandleMessages<>), typeof(ControllerModule).Assembly);

        container.Register<IInventoryLockManager, InventoryLockManager>(Lifestyle.Scoped);
        container.Register<IDistributedLockScopeHolder, DistributedLockScopeHolder>(Lifestyle.Scoped);

        container.RegisterInstance(serviceProvider.GetRequiredService<WorkflowOptions>());
        container.RegisterConditional<IOperationDispatcher, OperationDispatcher>(Lifestyle.Scoped, _ => true);
        container.RegisterConditional<IOperationTaskDispatcher, EryphTaskDispatcher>(Lifestyle.Scoped, _ => true);
        container.RegisterConditional<IOperationMessaging, EryphRebusOperationMessaging>(Lifestyle.Scoped,
            _ => true);
        container.AddRebusOperationsHandlers<OperationManager, OperationTaskManager>();

        container.AddStateDbDataServices();

        container.Register<IProjectNetworkPlanBuilder, ProjectNetworkPlanBuilder>(Lifestyle.Scoped);
        // KNOWN LIMITATION (deferred to the multi-host placement slice): the registry is
        // single-host (derives the agent from Environment.MachineName / local chassis). It is
        // correct for the all-in-one and the current single-host split dev runtime, but real
        // multi-host placement must derive host agents from the ComponentRegistration catalog
        // (IComponentRegistryService) instead. Not wired yet — same category as the documented
        // network-sync accepted workaround.
        container.RegisterSingleton<IComponentRegistry, SingleHostComponentRegistry>();
        container.RegisterSingleton<IClusterTopologyProvider, ComponentRegistryClusterTopologyProvider>();
        container.RegisterSingleton<INetworkSyncService, NetworkSyncService>();

        // Chooses the OVN northbound connection: the local pipe when the network process is
        // co-located (in-process or same host), or its advertised SSL endpoint when remote.
        container.Register<IOvnNorthboundConnectionProvider, OvnNorthboundConnectionProvider>(Lifestyle.Scoped);

        container.RegisterSingleton<IIdGenerator<long>>(IdGeneratorFactory.CreateIdGenerator);
        container.RegisterSingleton<IStorageIdentifierGenerator, StorageIdentifierGenerator>();

        // Placement and storage-agent location resolve through the component registry
        // (single instance shared across both interfaces).
        var agentLocator = Lifestyle.Singleton.CreateRegistration<ComponentRegistryAgentLocator>(container);
        container.AddRegistration(typeof(IPlacementCalculator), agentLocator);
        container.AddRegistration(typeof(IStorageManagementAgentLocator), agentLocator);

        // Component registration + configuration distribution (controller is the authority).
        container.Register<IComponentRegistryService, ComponentRegistryService>(Lifestyle.Scoped);
        // Broker user management is host-supplied: empty here (no managed broker, e.g. eryph-zero),
        // the split-runtime controller host appends a RabbitMQ provisioner so decommissioning a
        // component deletes its broker user. The decommission handler resolves whatever is registered.
        container.Collection.Register<IComponentBrokerProvisioner>(Array.Empty<Type>());
        container.Register<ConfigDistributionService>(Lifestyle.Scoped);
        // Controller settings (incl. the Placement section) are owned by the host.
        container.RegisterInstance(serviceProvider.GetRequiredService<IControllerSettingsManager>());
        // EndpointsConfigSource reads the operator endpoint overrides from the host
        // configuration; register it explicitly rather than relying on auto cross-wiring.
        container.RegisterInstance(_configuration);
        container.Collection.Register<IConfigSource>(typeof(PlacementConfigSource),
            typeof(NetworkProvidersConfigSource), typeof(EndpointsConfigSource));

        //use network services from host
        container.RegisterInstance(serviceProvider.GetRequiredService<INetworkProviderManager>());

        container.ConfigureRebus(configurer => configurer
            .Serialization(s => s.UseEryphSettings())
            .Transport(t =>
                container.GetInstance<IRebusTransportConfigurer>()
                    .Configure(t, QueueNames.Controllers))
            .Options(x =>
            {
                x.RetryStrategy(secondLevelRetriesEnabled: true, errorDetailsHeaderMaxLength: 5);
                x.SetNumberOfWorkers(5);
                x.EnableSimpleInjectorUnitOfWork();
                x.EnableOperationCancellation(
                    container.GetInstance<WorkflowOptions>(),
                    container.GetInstance<ITaskCancellationRegistry>());
            })
            .Timeouts(t => container.GetInstance<IRebusConfigurer<ITimeoutManager>>().Configure(t))
            .Sagas(s =>
            {
                container.GetInstance<IRebusConfigurer<ISagaStorage>>().Configure(s);
                s.EnforceExclusiveAccess();
            })
            .Subscriptions(s => container.GetService<IRebusConfigurer<ISubscriptionStorage>>()?.Configure(s))
            .Logging(x => x.MicrosoftExtensionsLogging(container.GetInstance<ILoggerFactory>()))
            .Start());

        container.Register(serviceProvider.GetRequiredService<IStateStoreContextConfigurer>);
    }

    [UsedImplicitly]
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
    {
        // Change tracking must be registered before seeding so its
        // hosted services run StartAsync (which enables the queues)
        // before SeedFromConfigHandler.StartAsync saves any changes.
        // Otherwise seeded changes would be enqueued against a disabled
        // queue and silently dropped, and seed-derived data (e.g. the
        // v0.4 -> v2 metadata salvage in CatletMetadataSeeder) would
        // never be persisted back to disk.
        if (_changeTrackingConfig.TrackChanges)
            options.AddChangeTracking();

        options.AddSeeding(_changeTrackingConfig);

        options.AddStartupHandler<SyncNetworksHandler>();
        options.AddStartupHandler<StartBusModuleHandler>();
        options.AddLogging();
    }
}
