using System;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.OVN;
using Eryph.Core;
using Eryph.Messages;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Modules.Controller.IdGenerator;
using Eryph.Modules.Controller.Inventory;
using Eryph.Modules.Controller.Networks;
using Eryph.Modules.Controller.Operations;
using Eryph.Rebus;
using Eryph.StateDb;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Handlers;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Sagas.Exclusive;
using Rebus.Serialization.Json;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;

namespace Eryph.Modules.Controller
{
    [UsedImplicitly]
    public class ControllerModule
    {
        public string Name => "Eryph.Controller";


        public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.Register<StartBusModuleHandler>();

            container.Register<IRebusUnitOfWork, StateStoreDbUnitOfWork>(Lifestyle.Scoped);
            container.Collection.Register(typeof(IHandleMessages<>), typeof(ControllerModule).Assembly);
            container.Collection.Append(typeof(IHandleMessages<>), typeof(IncomingTaskMessageHandler<>));
            container.Collection.Append(typeof(IHandleMessages<>), typeof(FailedOperationHandler<>));


            container.Register(typeof(IReadonlyStateStoreRepository<>), typeof(ReadOnlyStateStoreRepository<>), Lifestyle.Scoped);
            container.Register(typeof(IStateStoreRepository<>), typeof(StateStoreRepository<>), Lifestyle.Scoped);
            container.Register<IStateStore, StateStore>(Lifestyle.Scoped);

            container.Register<IVirtualMachineDataService, VirtualMachineDataService>(Lifestyle.Scoped);

            container.Register<IVirtualMachineMetadataService, VirtualMachineMetadataService>(Lifestyle.Scoped);
            container.Register<IVMHostMachineDataService, VMHostMachineDataService>(Lifestyle.Scoped);
            container.Register<IVirtualDiskDataService, VirtualDiskDataService>(Lifestyle.Scoped);
            container.Register<IProjectNetworkPlanBuilder, ProjectNetworkPlanBuilder>(Lifestyle.Scoped);

            container.Register<ICatletIpManager, CatletIpManager>(Lifestyle.Scoped);
            container.Register<IIpPoolManager, IpPoolManager>(Lifestyle.Scoped);


            container.RegisterSingleton(() => new Id64Generator());
            container.Register<IOperationTaskDispatcher, OperationDispatcher>();
            container.Register<IOperationDispatcher, OperationDispatcher>();

            //use placement calculator of Host
            container.Register(serviceProvider.GetRequiredService<IPlacementCalculator>);
            container.Register(serviceProvider.GetRequiredService<IStorageManagementAgentLocator>);

            //use network services from host
            container.RegisterInstance(serviceProvider.GetRequiredService<INetworkProviderManager>());
            container.RegisterInstance(serviceProvider.GetRequiredService<IOVNSettings>());
            container.RegisterInstance(serviceProvider.GetRequiredService<ISysEnvironment>());


            container.Register(() =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<StateStoreContext>();
                serviceProvider.GetRequiredService<IDbContextConfigurer<StateStoreContext>>().Configure(optionsBuilder);
                return new StateStoreContext(optionsBuilder.Options);
            }, Lifestyle.Scoped);

            container.ConfigureRebus(configurer => configurer
                .Transport(t =>
                    serviceProvider.GetRequiredService<IRebusTransportConfigurer>()
                        .Configure(t, QueueNames.Controllers))
                .Routing(r => r.TypeBased()
                        .Map(MessageTypes.ByRecipient(MessageRecipient.Controllers), QueueNames.Controllers)
                    // agent routing is not registered here by design. Agent commands will be routed by agent name
                )
                .Options(x =>
                {
                    x.SimpleRetryStrategy(secondLevelRetriesEnabled: true, errorDetailsHeaderMaxLength: 5);
                    x.SetNumberOfWorkers(5);
                    x.EnableSimpleInjectorUnitOfWork();
                })
                .Timeouts(t => serviceProvider.GetRequiredService<IRebusTimeoutConfigurer>().Configure(t))
                .Sagas(s =>
                {
                    serviceProvider.GetRequiredService<IRebusSagasConfigurer>().Configure(s);
                    s.EnforceExclusiveAccess();
                })
                .Subscriptions(s => serviceProvider.GetRequiredService<IRebusSubscriptionConfigurer>().Configure(s))
                //.Logging(x => x.Trace())
                .Start());
                
            
        }

        [UsedImplicitly]
        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            options.Services.AddHostedHandler<StartBusModuleHandler>();
            options.Services.AddHostedHandler<RealizeNetworkProviderHandler>();
            options.AddHostedService<InventoryTimerService>();
            options.AddLogging();
        }

    }
}