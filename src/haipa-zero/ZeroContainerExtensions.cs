using Haipa.IdentityDb;
using Haipa.Modules.Api;
using Haipa.Modules.Controller;
using Haipa.Modules.Hosting;
using Haipa.Modules.Identity;
using Haipa.Modules.Identity.Demo;
using Haipa.Modules.VmHostAgent;
using Haipa.Rebus;
using Haipa.StateDb;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;
using SimpleInjector;
using System;
using System.IO;

namespace Haipa.Runtime.Zero
{
    internal static class ZeroContainerExtensions
    {
        public static void Bootstrap(this Container container, string[] args)
        {
            container.HostModules()
                .AddModule<ApiModule>()
                .AddModule<IdentityModule>()
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
            //container.Register<IdentityDb.IDbContextConfigurer<IdentityDbContext>, InMemoryIdentityDbContextConfigurer>();
            container.Register<IdentityDb.IDbContextConfigurer<ConfigurationStoreContext>, InMemoryConfigurationStoreContextConfigurer>();
                 

            return container;
        }
       
    }
}
