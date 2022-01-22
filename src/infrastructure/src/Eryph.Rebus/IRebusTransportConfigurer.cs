using Rebus.Config;
using Rebus.Transport;

namespace Eryph.Rebus
{
    public interface IRebusTransportConfigurer
    {
        void ConfigureAsOneWayClient(StandardConfigurer<ITransport> configurer);
        void Configure(StandardConfigurer<ITransport> configurer, string queueName);
    }
}