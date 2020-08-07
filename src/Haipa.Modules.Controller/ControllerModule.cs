using System;
using Dbosoft.Hosuto.HostedServices;
using Dbosoft.Hosuto.Modules;
using Haipa.Messages;
using Haipa.Modules.Controller.IdGenerator;
using Haipa.Modules.Controller.Inventory;
using Haipa.Modules.Controller.Operations;
using Haipa.Rebus;
using Haipa.StateDb;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Rebus.Handlers;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Haipa.Modules.Controller
{
    [UsedImplicitly]
    public class ControllerModule : IModule
    {
        public string Name => "Haipa.Controller";


        public void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.Register<StartBusModuleHandler>();
            container.Register<InventoryHandler>();

            container.Collection.Register(typeof(IHandleMessages<>), typeof(ControllerModule).Assembly);
            container.Collection.Append(typeof(IHandleMessages<>), typeof(IncomingOperationTaskHandler<>));

            container.RegisterSingleton( () => new Id64Generator());
            container.Register<IOperationTaskDispatcher, OperationTaskDispatcher>();

            //use placement calculator of Host
            container.Register(serviceProvider.GetService<IPlacementCalculator>);


            container.Register(() =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<StateStoreContext>();
                serviceProvider.GetService<IDbContextConfigurer<StateStoreContext>>().Configure(optionsBuilder);
                return new StateStoreContext(optionsBuilder.Options);
            }, Lifestyle.Scoped);

            container.ConfigureRebus(configurer => configurer
                .Transport(t => serviceProvider.GetService<IRebusTransportConfigurer>().Configure(t, QueueNames.Controllers))
                .Routing(r => r.TypeBased()
                    .Map(MessageTypes.ByRecipient(MessageRecipient.Controllers), QueueNames.Controllers)
                    // agent routing is not registered here by design. Agent commands will be routed by agent name
                    )
                .Options(x =>
                {
                    x.SimpleRetryStrategy();
                    x.SetNumberOfWorkers(5);
                })
                .Timeouts(t => serviceProvider.GetService<IRebusTimeoutConfigurer>().Configure(t))
                .Sagas(s => serviceProvider.GetService<IRebusSagasConfigurer>().Configure(s))
                .Subscriptions(s => serviceProvider.GetService<IRebusSubscriptionConfigurer>().Configure(s))

                .Serialization(x => x.UseNewtonsoftJson(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None }))
                .Logging(x => x.ColoredConsole(LogLevel.Debug)).Start());
        }

        public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.AddHostedHandler<StartBusModuleHandler>();
            services.AddHostedHandler<InventoryHandler>();

        }




    }
}
