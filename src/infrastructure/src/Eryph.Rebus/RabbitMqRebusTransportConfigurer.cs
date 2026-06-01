using System;
using System.Net.Security;
using System.Security.Authentication;
using Dbosoft.Rebus.Configuration;
using Rebus.Config;
using Rebus.RabbitMq;
using Rebus.Transport;

namespace Eryph.Rebus
{
    /// <summary>
    /// Configures the RabbitMQ transport. When a client-certificate file is supplied (the split
    /// runtime with bus mTLS), the connection uses TLS, presents the component's client certificate
    /// and validates the broker certificate against the host's trust store. Without it the
    /// connection is plaintext (e.g. the dev path).
    /// </summary>
    /// <remarks>
    /// Rebus.RabbitMq configures TLS per connection endpoint from <see cref="SslSettings"/>, which
    /// only accepts a certificate <i>file</i> path and validates the server against the operating
    /// system trust store (an in-memory certificate or a custom validation callback set on the
    /// connection factory is not honoured — the connection is opened from per-endpoint SslOptions,
    /// not the factory's). The deployment's root CA is therefore installed into the host trust store
    /// by provisioning, and the client certificate is supplied as an (ACL-protected) PKCS#12 file.
    /// </remarks>
    public class RabbitMqRebusTransportConfigurer : IRebusTransportConfigurer
    {
        private readonly string _clientCertificatePfxPath;
        private readonly string _clientCertificatePassphrase;

        public RabbitMqRebusTransportConfigurer()
        {
        }

        public RabbitMqRebusTransportConfigurer(
            string clientCertificatePfxPath,
            string clientCertificatePassphrase = "")
        {
            _clientCertificatePfxPath = clientCertificatePfxPath;
            _clientCertificatePassphrase = clientCertificatePassphrase ?? "";
        }

        public void ConfigureAsOneWayClient(StandardConfigurer<ITransport> configurer)
        {
            WaitForConnection();
            ApplyMutualTls(configurer.UseRabbitMqAsOneWayClient(ConnectionString()));
        }

        public void Configure(StandardConfigurer<ITransport> configurer, string queueName)
        {
            WaitForConnection();
            ApplyMutualTls(configurer.UseRabbitMq(ConnectionString(), queueName));
        }

        private static string ConnectionString()
        {
            var connectionString = Environment.GetEnvironmentVariable("RABBITMQ_CONNECTIONSTRING");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "RABBITMQ_CONNECTIONSTRING must be set to connect to the message bus.");
            return connectionString;
        }

        private void ApplyMutualTls(RabbitMqOptionsBuilder options)
        {
            if (string.IsNullOrEmpty(_clientCertificatePfxPath))
                return;

            // ServerName must match the broker certificate; it is the host the client connects to.
            // ConnectionString() has already validated it is set.
            var serverName = new Uri(ConnectionString()).Host;
            options.Ssl(new SslSettings(
                enabled: true,
                serverName: serverName,
                certificatePath: _clientCertificatePfxPath,
                certPassphrase: _clientCertificatePassphrase,
                // Let the OS negotiate the protocol (TLS 1.2/1.3) and require a fully valid server
                // chain — the deployment root CA is installed into the host trust store.
                version: SslProtocols.None,
                acceptablePolicyErrors: SslPolicyErrors.None));
        }

        private void WaitForConnection()
        {
            RabbitMqConnectionCheck.WaitForRabbitMq(new TimeSpan(0, 0, 10)).Wait();
        }
    }
}
