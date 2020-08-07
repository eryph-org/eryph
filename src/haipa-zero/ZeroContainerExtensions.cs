using Haipa.IdentityDb;
using Haipa.Modules.Api;
using Haipa.Modules.Controller;
using Haipa.Modules.Identity;
using Haipa.Modules.Identity.Seeder;
using Haipa.Modules.VmHostAgent;
using Haipa.Rebus;
using Haipa.StateDb;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;
using SimpleInjector;
using System;
using System.Collections.Generic;
using System.IO;

namespace Haipa.Runtime.Zero
{
    internal static class ZeroContainerExtensions
    {
        public static void Bootstrap(this Container container)
        {
            container
                .UseInMemoryBus()
                .UseInMemoryDb();

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
            container.Register<IdentityDb.IDbContextConfigurer<ConfigurationStoreContext>, InMemoryConfigurationStoreContextConfigurer>();
            return container;
        }

    }
}
