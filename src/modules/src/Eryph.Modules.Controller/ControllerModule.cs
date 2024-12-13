using System;
using System.IO.Abstractions;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.OVN;
using Dbosoft.Rebus;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Configuration;
using Eryph.Core;
using Eryph.DistributedLock;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Configuration;
using Eryph.ModuleCore.Startup;
using Eryph.Modules.Controller.ChangeTracking;
using Eryph.Modules.Controller.DataServices;
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

namespace Eryph.Modules.Controller
{
    [UsedImplicitly]
    public class ControllerModule
    {
        private readonly ChangeTrackingConfig _changeTrackingConfig = new();

        public string Name => "Eryph.Controller";

        public ControllerModule(IConfiguration configuration)
        {
             configuration.GetSection("ChangeTracking")
                .Bind(_changeTrackingConfig);
        }

        [UsedImplicitly]
        public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.AddTransient<InventoryTimerJob>();
            services.AddQuartz(q =>
            {
                q.SchedulerName = $"{Name}.Scheduler";

                q.AddJob<InventoryTimerJob>(
                    job => job.WithIdentity(InventoryTimerJob.Key)
                        .DisallowConcurrentExecution());

                q.AddTrigger(trigger => trigger.WithIdentity("InventoryTimerJobTrigger")
                    .ForJob(InventoryTimerJob.Key)
                    .StartNow()
                    .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromMinutes(10)).RepeatForever()));

                // The scheduled trigger will only fire the first time after 10 minutes.
                // We add another trigger without a schedule to trigger the job immediately
                // when the scheduler starts.
                q.AddTrigger(trigger => trigger.WithIdentity("InventoryTimerJobStartupTrigger")
                    .ForJob(InventoryTimerJob.Key)
                    .StartNow());
            });
            services.AddQuartzHostedService();
        }

        [UsedImplicitly]
        public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.RegisterSingleton<IFileSystem, FileSystem>();
            container.RegisterInstance(_changeTrackingConfig);

            container.Register<IRebusUnitOfWork, StateStoreDbUnitOfWork>(Lifestyle.Scoped);
            container.Collection.Register(typeof(IHandleMessages<>), typeof(ControllerModule).Assembly);

            container.Register<IInventoryLockManager, InventoryLockManager>(Lifestyle.Scoped);
            container.Register<IDistributedLockScopeHolder, DistributedLockScopeHolder>(Lifestyle.Scoped);

            container.RegisterInstance(serviceProvider.GetRequiredService<WorkflowOptions>());
            container.RegisterConditional<IOperationDispatcher, OperationDispatcher>(Lifestyle.Scoped, _ => true);
            container.RegisterConditional<IOperationTaskDispatcher, EryphTaskDispatcher>(Lifestyle.Scoped, _ => true);
            container.RegisterConditional<IOperationMessaging, EryphRebusOperationMessaging>(Lifestyle.Scoped, _ => true);
            container.AddRebusOperationsHandlers<OperationManager, OperationTaskManager>();

            container.Register<IVirtualMachineDataService, VirtualMachineDataService>(Lifestyle.Scoped);
            container.Register<IVirtualMachineMetadataService, VirtualMachineMetadataService>(Lifestyle.Scoped);
            container.Register<IVMHostMachineDataService, VMHostMachineDataService>(Lifestyle.Scoped);
            container.Register<IVirtualDiskDataService, VirtualDiskDataService>(Lifestyle.Scoped);
            container.Register<IProjectNetworkPlanBuilder, ProjectNetworkPlanBuilder>(Lifestyle.Scoped);

            container.Register<ICatletIpManager, CatletIpManager>(Lifestyle.Scoped);
            container.Register<IProviderIpManager, ProviderIpManager>(Lifestyle.Scoped);
            container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);
            container.Register<INetworkConfigValidator, NetworkConfigValidator>(Lifestyle.Scoped);
            container.Register<INetworkConfigRealizer, NetworkConfigRealizer>(Lifestyle.Scoped);
            container.Register<IDefaultNetworkConfigRealizer, DefaultNetworkConfigRealizer>(Lifestyle.Scoped);
            container.Register<INetworkProvidersConfigRealizer, NetworkProvidersConfigRealizer>(Lifestyle.Scoped);
            container.RegisterSingleton<INetworkSyncService, NetworkSyncService>();

            container.RegisterSingleton<IIdGenerator<long>>(IdGeneratorFactory.CreateIdGenerator);

            //use placement calculator of Host
            container.RegisterInstance(serviceProvider.GetRequiredService<IPlacementCalculator>());
            container.RegisterInstance(serviceProvider.GetRequiredService<IStorageManagementAgentLocator>());

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
            options.AddSeeding(_changeTrackingConfig);
            
            if(_changeTrackingConfig.TrackChanges)
                options.AddChangeTracking();

            options.AddStartupHandler<SyncNetworksHandler>();
            options.AddStartupHandler<StartBusModuleHandler>();
            options.AddLogging();
        }

    }
}