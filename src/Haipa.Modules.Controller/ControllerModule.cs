using System;
using Haipa.Rebus;
using Haipa.StateDb;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Rebus.Handlers;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Serialization.Json;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Haipa.Modules.Controller
{
    [UsedImplicitly]
    public class ControllerModule : ModuleBase
    {
        public override string Name => "Haipa.Controller";


        protected override void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {

            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            container.Collection.Register(typeof(IHandleMessages<>), typeof(ControllerModule).Assembly);
            //container.Collection.Append(typeof(IHandleMessages<>), typeof(DispatchOperationHandler<>));

            container.RegisterSingleton( () => new Id64Generator());

            container.Register(() =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<StateStoreContext>();
                serviceProvider.GetService<IDbContextConfigurer<StateStoreContext>>().Configure(optionsBuilder);
                return new StateStoreContext(optionsBuilder.Options);
            }, Lifestyle.Scoped);

            container.ConfigureRebus(configurer => configurer
                .Transport(t => serviceProvider.GetService<IRebusTransportConfigurer>().Configure(t, "haipa.controller"))
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

        protected override void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.AddModuleService<InventoryModuleService>();

        }




    }
}
