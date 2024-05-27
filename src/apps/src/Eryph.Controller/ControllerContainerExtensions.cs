using Dbosoft.Rebus.Configuration;
using Eryph.Rebus;
using SimpleInjector;

namespace Eryph.Controller
{
    internal static class ControllerContainerExtensions
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
        //    container.Register<IRebusSagasConfigurer, MySqlSagaConfigurer>();
        //    container.Register<IRebusSubscriptionConfigurer, MySqlSubscriptionConfigurer>();
        //    container.Register<IRebusTimeoutConfigurer, MySqlTimeoutConfigurer>();

            //container.Register<IDbContextConfigurer<StateStoreContext>, MySqlDbContextConfigurer<StateStoreContext>>();
            return container;
        }
    }
}