using System.Threading.Tasks;
using Haipa.Modules.Abstractions;
using Haipa.Modules.Api;
using Haipa.Rebus;
using Haipa.StateDb;
using Haipa.StateDb.MySql;
using Microsoft.AspNetCore;
using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;
using SimpleInjector;

namespace Haipa.ApiEndpoint
{
    public class Program
    {

        public static Task Main(string[] args)
        {
            var container = new Container();

            container.Register<IWebModule, ApiModule>();
            container.RegisterInstance<IWebModuleHostBuilderFactory>(
                new PassThroughWebHostBuilderFactory(() => WebHost.CreateDefaultBuilder(args)));

            container.Register<IDbContextConfigurer<StateStoreContext>, MySqlDbContextConfigurer<StateStoreContext>>();

            container.RegisterInstance(new InMemNetwork(true));
            container.RegisterInstance(new InMemorySubscriberStore());
            container.Register<IRebusTransportConfigurer, InMemoryTransportConfigurer>();
            container.Register<IRebusSagasConfigurer, InMemorySagasConfigurer>();
            container.Register<IRebusSubscriptionConfigurer, InMemorySubscriptionConfigurer>();
            container.Register<IRebusTimeoutConfigurer, InMemoryTimeoutConfigurer>();

            container.Verify();

            return container.GetInstance<IWebModule>().RunAsync();

        }

    }
}
