using System;
using Rebus.Config;
using Rebus.Transport;

namespace Haipa.Rebus
{
    public class RabbitMqRebusTransportConfigurer : IRebusTransportConfigurer
    {
        public void ConfigureAsOneWayClient(StandardConfigurer<ITransport> configurer)
        {
            WaitForConnection();
            configurer.UseRabbitMqAsOneWayClient(
                Environment.GetEnvironmentVariable("RABBITMQ_CONNECTIONSTRING"));
        }

        public void Configure(StandardConfigurer<ITransport> configurer, string queueName)
        {
            WaitForConnection();
            configurer.UseRabbitMq(
                Environment.GetEnvironmentVariable("RABBITMQ_CONNECTIONSTRING"), queueName);
        }

        private void WaitForConnection()
        {
            RabbitMqConnectionCheck.WaitForRabbitMq(new TimeSpan(0, 0, 10)).Wait();
        }
    }
}