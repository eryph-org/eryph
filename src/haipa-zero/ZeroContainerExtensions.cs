using System.Linq;
using Haipa.Modules.Abstractions;
using Haipa.Modules.Api;
using Haipa.Modules.Controller;
using HyperVPlus.Rebus;
using HyperVPlus.StateDb;
using Microsoft.AspNetCore;
using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;
using SimpleInjector;

namespace Haipa.Runtime.Zero
{
    internal static class ZeroContainerExtensions
    {
        public static void Bootstrap(this Container container, string[] args)
        {
            var modules = new[]
            {
                typeof(ApiModule),
                typeof(ControllerModule),
                //typeof(VmHostAgentModule)
            };

            container.Collection.Register<IModule>(
                modules.Select(t => Lifestyle.Singleton.CreateRegistration(t, container)));

            container
                .HostAspNetCore(args)
                .UseInMemoryBus()
                .UseInMemoryDb();
        }

        public static Container HostAspNetCore(this Container container, string[] args)
        {
            container.RegisterInstance<IWebModuleHostBuilderFactory>(
                new PassThroughWebHostBuilderFactory(() => WebHost.CreateDefaultBuilder(args)));
            return container;
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
            container.Register<IDbContextConfigurer<StateStoreContext>, InMemoryStateStoreContextConfigurer>();

            return container;
        }
    }
}
