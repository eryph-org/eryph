using System;
using Rebus.Config;
using Rebus.Transport;

namespace HyperVPlus.Rebus
{
    class RabbitMqRebusTransportConfigurer : IRebusTransportConfigurer
    {
        void WaitForConnection()
        {
            RabbitMqConnectionCheck.WaitForRabbitMq(new TimeSpan(0, 0, 10)).Wait();

        }

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
    }
}