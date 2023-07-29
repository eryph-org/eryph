using System;
using System.Collections.Generic;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Dbosoft.Rebus.Configuration;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Rebus;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rebus.Persistence.InMem;
using Rebus.Sagas;
using Rebus.Subscriptions;
using Rebus.Timeouts;
using Rebus.Transport.InMem;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.ComputeApi.Tests.Integration
{
    public class ApiModuleFactory : WebModuleFactory<ComputeApiModule>
    {
        private readonly Container _container = new();
        
        protected override IModulesHostBuilder CreateModuleHostBuilder()
        {
            var moduleHostBuilder = new ModulesHostBuilder();
            _container.Options.AllowOverridingRegistrations = true;
            moduleHostBuilder.UseSimpleInjector(_container);

            _container.RegisterInstance<ILoggerFactory>(new NullLoggerFactory());
            _container.RegisterConditional(
                typeof(ILogger),
                c => typeof(Logger<>).MakeGenericType(c.Consumer!.ImplementationType),
                Lifestyle.Singleton,
                _ => true);

            moduleHostBuilder.UseEnvironment(Environments.Development);

            var endpoints = new Dictionary<string, string>
            {
                {"identity", "http://localhost/identity"},
                {"compute", "http://localhost/compute"},
                {"common", "http://localhost/common"},
                {"network", "http://localhost/network"},

            };

            moduleHostBuilder.ConfigureAppConfiguration(cfg =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "bus:type", "inmemory" },
                    { "databus:type", "inmemory" },
                    { "store:type", "inmemory" }
                });
            });


            _container.RegisterInstance(new WorkflowOptions
            {
                DispatchMode = WorkflowEventDispatchMode.Publish,
                EventDestination = QueueNames.Controllers,
                OperationsDestination = QueueNames.Controllers,
            });

            _container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));

            _container.RegisterInstance(new InMemNetwork());
            _container.RegisterInstance(new InMemorySubscriberStore());
            _container.Register<IRebusTransportConfigurer, DefaultTransportSelector>();
            _container.Register<IRebusConfigurer<ISagaStorage>, DefaultSagaStoreSelector>();
            _container.Register<IRebusConfigurer<ITimeoutManager>, DefaultTimeoutsStoreSelector>();
            _container.Register<IRebusConfigurer<ISubscriptionStorage>, DefaultSubscriptionStoreSelector>();

            _container.RegisterInstance(new InMemoryDatabaseRoot());
            _container.Register<IDbContextConfigurer<StateStoreContext>, InMemoryStateStoreContextConfigurer>();

            return moduleHostBuilder;
        }

        public WebModuleFactory<ComputeApiModule> SetupStateStore(Action<StateStoreContext> configure)
        {

            var factory = WithModuleConfiguration(options =>
            {
                options.Configure(cfg =>
                {
                    var container = cfg.Services.GetRequiredService<Container>();
                    using var scope = AsyncScopedLifestyle.BeginScope(container);

                    var stateStore = scope.GetInstance<StateStoreContext>();
                    configure(stateStore);
                    stateStore.SaveChanges();
                });
            });

            return factory;
        }

        protected override void Dispose(bool disposing)
        {
            _container?.Dispose();
            base.Dispose(disposing);
        }
    }
}