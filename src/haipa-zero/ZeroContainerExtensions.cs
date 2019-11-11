using System;
using Haipa.IdentityDb;
using Haipa.Modules;
using Haipa.Modules.Api;
using Haipa.Modules.Controller;
using Haipa.Modules.Hosting;
using Haipa.Modules.Identity;
using Haipa.Modules.Identity.Services;
using Haipa.Modules.VmHostAgent;
using Haipa.Rebus;
using Haipa.Runtime.Zero.ConfigStore;
using Haipa.Runtime.Zero.ConfigStore.Clients;
using Haipa.StateDb;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;
using SimpleInjector;
using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.Extensions.DependencyInjection;

namespace Haipa.Runtime.Zero
{
    internal static class ZeroContainerExtensions
    {
        public static void Bootstrap(this Container container, string[] args)
        {
            container.HostModules()
                .AddModule<ApiModule>()
                .AddIdentityModule()
                .AddModule<VmHostAgentModule>()
                .AddModule<ControllerModule>();

            container
                .HostAspNetCore((path) =>
                {
                    return WebHost.CreateDefaultBuilder(args)
                        .UseHttpSys(options =>
                        {
                            options.UrlPrefixes.Add($"https://localhost:62189/{path}");
                        })
                        .UseUrls($"https://localhost:62189/{path}")
                        .UseEnvironment("Development")

                        .ConfigureLogging(lc => lc.SetMinimumLevel(LogLevel.Warning));
                });
            container
                .UseInMemoryBus()
                .UseInMemoryDb();

            container.RegisterConditional(typeof(IModuleHost<>), typeof(ModuleHost<>), c => !c.Handled);

            container.Register<IPlacementCalculator, ZeroAgentPlacementCalculator>();


        }
        public static Container UseInMemoryBus(this Container container)
        {
            container.RegisterInstance(new InMemNetwork(true));
            container.RegisterInstance(new InMemorySubscriberStore());
            container.Register<IRebusTransportConfigurer, InMemoryTransportConfigurer>();
            container.Register<IRebusSagasConfigurer, InMemorySagasConfigurer>();
            container.Register<IRebusSubscriptionConfigurer, InMemorySubscriptionConfigurer>();
            container.Register<IRebusTimeoutConfigurer, InMemoryTimeoutConfigurer>(); return container;
        }
        public static Container UseInMemoryDb(this Container container)
        {
            container.RegisterInstance(new InMemoryDatabaseRoot());
            container.Register<StateDb.IDbContextConfigurer<StateStoreContext>, InMemoryStateStoreContextConfigurer>();
            return container;
        }

        public static IModuleHostBuilder AddIdentityModule(this IModuleHostBuilder builder)
        {
            builder.AddModule<IdentityModule>();
            
            builder.Container.RegisterSingleton<IConfigReaderService<ClientConfigModel>, ClientConfigReaderService>();
            builder.Container.RegisterSingleton<IConfigWriterService<ClientConfigModel>, ClientConfigWriterService>();
            builder.Container.Register<IContainerConfigurer<IdentityModule>, IdentityModuleContainerConfigurer>();
            builder.Container.Register<IServicesConfigurer<IdentityModule>, IdentityModuleServicesConfigurer>();

            builder.Container.Register<IdentityDb.IDbContextConfigurer<ConfigurationDbContext>, InMemoryConfigurationStoreContextConfigurer>();

            return builder;
        }


        private class IdentityModuleContainerConfigurer : IContainerConfigurer<IdentityModule>
        {
            public void ConfigureContainer(IdentityModule module, IServiceProvider serviceProvider, Container container)
            {
                container.Register(serviceProvider.GetRequiredService<IConfigWriterService<ClientConfigModel>>);
                container.Register(serviceProvider.GetRequiredService<IConfigReaderService<ClientConfigModel>>);
                container.RegisterDecorator<IClientService, ClientServiceWithConfigServiceDecorator>();
                container.Collection.Append<IConfigSeeder<IdentityModule>, IdentityClientSeeder>();

            }
        }

        private class IdentityModuleServicesConfigurer : IServicesConfigurer<IdentityModule>
        {
            public void ConfigureServices(IdentityModule module, IServiceProvider serviceProvider, IServiceCollection services)
            {
                services.AddScopedModuleHandler<SeedFromConfigHandler<IdentityModule>>();
            }
        }
    }

}
