using System.Threading.Tasks;
using Haipa.Modules;
using Haipa.Modules.Api;
using Haipa.Modules.Hosting;
using Haipa.Rebus;
using Haipa.StateDb;
using Haipa.StateDb.MySql;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
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

            container.HostAspNetCore((path) => WebHost.CreateDefaultBuilder(args));
            container.HostModule<ApiModule>();

            container.Register<IDbContextConfigurer<StateStoreContext>, MySqlDbContextConfigurer<StateStoreContext>>();

            container.RegisterInstance(new InMemNetwork(true));
            container.RegisterInstance(new InMemorySubscriberStore());
            container.Register<IRebusTransportConfigurer, InMemoryTransportConfigurer>();
            container.Register<IRebusSagasConfigurer, InMemorySagasConfigurer>();
            container.Register<IRebusSubscriptionConfigurer, InMemorySubscriptionConfigurer>();
            container.Register<IRebusTimeoutConfigurer, InMemoryTimeoutConfigurer>();

            container.Verify();

            return container.RunModule<ApiModule>();


        }

    }
}
