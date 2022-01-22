using Rebus.Config;
using Rebus.Transport;
using Rebus.Transport.InMem;

namespace Eryph.Rebus
{
    public class InMemoryTransportConfigurer : IRebusTransportConfigurer
    {
        private readonly InMemNetwork _network;

        public InMemoryTransportConfigurer(InMemNetwork network)
        {
            _network = network;
        }

        public void ConfigureAsOneWayClient(StandardConfigurer<ITransport> configurer)
        {
            configurer.UseInMemoryTransportAsOneWayClient(_network);
        }

        public void Configure(StandardConfigurer<ITransport> configurer, string queueName)
        {
            configurer.UseInMemoryTransport(_network, queueName);
        }
    }
}