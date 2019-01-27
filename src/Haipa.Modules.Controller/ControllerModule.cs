using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Modules.Abstractions;
using Haipa.Rebus;
using Haipa.StateDb;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Serialization.Json;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Haipa.Modules.Controller
{
    [UsedImplicitly]
    public class ControllerModule : IModule
    {
        public string Name => "Haipa.Controller";

        private readonly Container _globalContainer;

        public ControllerModule(Container globalContainer)
        {
            _globalContainer = globalContainer;
        }
        
        public void Start()
        {
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            container.Collection.Register(typeof(IHandleMessages<>), typeof(ControllerModule).Assembly);
            //container.Collection.Append(typeof(IHandleMessages<>), typeof(DispatchOperationHandler<>));

            container.Register(() =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<StateStoreContext>();
                _globalContainer.GetInstance<IDbContextConfigurer<StateStoreContext>>().Configure(optionsBuilder);
                return new StateStoreContext(optionsBuilder.Options);
            }, Lifestyle.Scoped);

            container.ConfigureRebus(configurer => configurer
                .Transport(t => _globalContainer.GetInstance<IRebusTransportConfigurer>().Configure(t, "haipa.controller"))
                .Options(x =>
                {
                    x.SimpleRetryStrategy();
                    x.SetNumberOfWorkers(5);
                })
                .Timeouts(t => _globalContainer.GetInstance<IRebusTimeoutConfigurer>().Configure(t))
                .Sagas(s => _globalContainer.GetInstance<IRebusSagasConfigurer>().Configure(s))
                .Subscriptions(s => _globalContainer.GetInstance<IRebusSubscriptionConfigurer>().Configure(s))

                .Serialization(x => x.UseNewtonsoftJson(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None }))
                .Logging(x => x.ColoredConsole(LogLevel.Debug)).Start());

            container.StartBus();

            container.GetInstance<IBus>().Advanced.Topics.Publish("agent.all", new InventoryRequestedEvent());

        }

        public void Stop()
        {
        }
    }


}
