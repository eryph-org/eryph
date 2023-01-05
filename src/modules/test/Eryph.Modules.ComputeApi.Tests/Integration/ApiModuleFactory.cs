using System;
using System.Collections.Generic;
using Dbosoft.Hosuto.Modules.Hosting;
using Dbosoft.Hosuto.Modules.Testing;
using Eryph.ModuleCore;
using Eryph.Rebus;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Persistence.InMem;
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

            moduleHostBuilder.UseEnvironment(Environments.Development);

            var endpoints = new Dictionary<string, string>
            {
                {"identity", "http://localhost/identity"},
                {"compute", "http://localhost/compute"},
                {"common", "http://localhost/common"},
                {"network", "http://localhost/network"},

            };

            _container.RegisterInstance<IEndpointResolver>(new EndpointResolver(endpoints));

            _container.RegisterInstance(new InMemNetwork());
            _container.RegisterInstance(new InMemorySubscriberStore());
            _container.Register<IRebusTransportConfigurer, InMemoryTransportConfigurer>();
            _container.Register<IRebusSagasConfigurer, InMemorySagasConfigurer>();
            _container.Register<IRebusSubscriptionConfigurer, InMemorySubscriptionConfigurer>();
            _container.Register<IRebusTimeoutConfigurer, InMemoryTimeoutConfigurer>();

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
    }
}