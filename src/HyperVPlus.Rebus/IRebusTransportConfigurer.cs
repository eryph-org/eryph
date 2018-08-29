using RabbitMQ.Client.MessagePatterns;
using Rebus.Config;
using Rebus.Transport;

namespace HyperVPlus.Rebus
{
    public interface IRebusTransportConfigurer
    {
        void ConfigureAsOneWayClient(StandardConfigurer<ITransport> configurer);
        void Configure(StandardConfigurer<ITransport> configurer, string queueName);
    }
}