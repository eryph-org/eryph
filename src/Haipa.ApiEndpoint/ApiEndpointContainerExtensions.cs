using Haipa.Modules.Api;
using Haipa.Modules.Hosting;
using Haipa.Rebus;
using Haipa.StateDb;
using Haipa.StateDb.MySql;
using Microsoft.AspNetCore;
using SimpleInjector;

namespace Haipa.ApiEndpoint
{
    internal static class ApiEndpointContainerExtensions
    {
        public static void Bootstrap(this Container container, string[] args)
        {
            container.HostModules().AddModule<ApiModule>();

            container.HostAspNetCore((path) => WebHost.CreateDefaultBuilder(args));


            container
                .UseRabbitMq()
                .UseMySql();
        }

        public static Container UseRabbitMq(this Container container)
        {
            container.Register<IRebusTransportConfigurer, RabbitMqRebusTransportConfigurer>();
            
            return container;
        }

        public static Container UseMySql(this Container container)
        {
            container.Register<IDbContextConfigurer<StateStoreContext>, MySqlDbContextConfigurer<StateStoreContext>>();

            return container;
        }
    }
}
