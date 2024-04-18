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
using Eryph.ModuleCore;
using Eryph.ModuleCore.Configuration;
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
        private readonly ChangeTrackingConfig _changeTrackingConfig;

        public string Name => "Eryph.Controller";

        public ControllerModule(IConfiguration configuration)
        {
            _changeTrackingConfig = configuration
                .GetSection("ChangeTracking")
                .Get<ChangeTrackingConfig>();
        }

        public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.RegisterSingleton<IFileSystem, FileSystem>();
            container.RegisterInstance(_changeTrackingConfig);

            container.Register<StartBusModuleHandler>();

            container.Register<IRebusUnitOfWork, StateStoreDbUnitOfWork>(Lifestyle.Scoped);
            container.Collection.Register(typeof(IHandleMessages<>), typeof(ControllerModule).Assembly);

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
            container.Register(serviceProvider.GetRequiredService<IPlacementCalculator>);
            container.Register(serviceProvider.GetRequiredService<IStorageManagementAgentLocator>);

            //use network services from host
            container.RegisterInstance(serviceProvider.GetRequiredService<INetworkProviderManager>());
            container.RegisterInstance(serviceProvider.GetRequiredService<IOVNSettings>());
            container.RegisterInstance(serviceProvider.GetRequiredService<ISysEnvironment>());

            container.ConfigureRebus(configurer => configurer
                .Transport(t =>
                    serviceProvider.GetRequiredService<IRebusTransportConfigurer>()
                        .Configure(t, QueueNames.Controllers))
                .Options(x =>
                {
                    x.RetryStrategy(secondLevelRetriesEnabled: true, errorDetailsHeaderMaxLength: 5);
                    x.SetNumberOfWorkers(5);
                    x.EnableSimpleInjectorUnitOfWork();
                })
                .Timeouts(t => serviceProvider.GetRequiredService<IRebusConfigurer<ITimeoutManager>>().Configure(t))
                .Sagas(s =>
                {
                    serviceProvider.GetRequiredService<IRebusConfigurer<ISagaStorage>>().Configure(s);
                    s.EnforceExclusiveAccess();
                })
                .Subscriptions(s => serviceProvider.GetRequiredService<IRebusConfigurer<ISubscriptionStorage>>().Configure(s))
                .Logging(x => x.Serilog())
                .Start());
        }

        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            options.AddSeeding(_changeTrackingConfig);
            
            if(_changeTrackingConfig.TrackChanges)
                options.AddChangeTracking();
            
            options.Services.AddHostedHandler<StartBusModuleHandler>();
            options.Services.AddHostedHandler<SyncNetworksHandler>();
            options.AddHostedService<InventoryTimerService>();
            options.AddLogging();
            options.RegisterStateStore();
        }

    }
}