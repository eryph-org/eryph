using Eryph.Rebus;
using Eryph.StateDb;
using Eryph.StateDb.MySql;
using SimpleInjector;

namespace Eryph.ApiEndpoint
{
    internal static class ApiEndpointContainerExtensions
    {
        public static void Bootstrap(this Container container)
        {
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