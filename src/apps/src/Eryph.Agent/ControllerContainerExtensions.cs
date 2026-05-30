using Eryph.Rebus;
using SimpleInjector;

namespace Eryph.Agent
{
    internal static class ControllerContainerExtensions
    {
        public static void Bootstrap(this Container container)
        {
            container
                .UseRabbitMq();
        }

        public static Container UseRabbitMq(this Container container)
        {
            container.Register<IRebusTransportConfigurer, RabbitMqRebusTransportConfigurer>();

            return container;
        }
    }
}