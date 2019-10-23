using Haipa.Controller.Rebus;
using Haipa.Modules.Controller;
using Haipa.Modules.Hosting;
using Haipa.Rebus;
using Haipa.StateDb;
using Haipa.StateDb.MySql;
using SimpleInjector;

namespace Haipa.Controller
{
    internal static class ControllerContainerExtensions
    {
        public static void Bootstrap(this Container container, string[] args)
        {
            container.HostModule<ControllerModule>();

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
            container.Register<IRebusSagasConfigurer, MySqlSagaConfigurer>();
            container.Register<IRebusSubscriptionConfigurer, MySqlSubscriptionConfigurer>();
            container.Register<IRebusTimeoutConfigurer, MySqlTimeoutConfigurer>();

            container.Register<IDbContextConfigurer<StateStoreContext>, MySqlDbContextConfigurer<StateStoreContext>>();

            return container;
        }
    }
}
