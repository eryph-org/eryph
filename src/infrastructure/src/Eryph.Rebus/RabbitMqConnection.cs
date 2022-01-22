using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Eryph.Rebus
{
    public static class RabbitMqConnectionCheck
    {
        public static string ConnectionString => Environment.GetEnvironmentVariable("RABBITMQ_CONNECTIONSTRING");

        public static async Task WaitForRabbitMq(TimeSpan timeout)
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                throw new ApplicationException(
                    "missing RABBITMQ connection string (set environment variable RABBITMQ_CONNECTIONSTRING");

            var factory = new ConnectionFactory {Uri = new Uri(ConnectionString)};

            var cancellationSource = new CancellationTokenSource(timeout);
            while (!cancellationSource.IsCancellationRequested)
            {
                try
                {
                    using (var conn = factory.CreateConnection())
                    {
                        using (var model = conn.CreateModel())
                        {
                            if (model.IsOpen)
                                return;
                        }
                    }
                }
                catch (BrokerUnreachableException)
                {
                }

                // ReSharper disable once MethodSupportsCancellation
                await Task.Delay(100).ConfigureAwait(false);
            }
        }
    }
}