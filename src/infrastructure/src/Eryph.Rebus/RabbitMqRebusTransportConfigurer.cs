using System;
using System.Security.Cryptography.X509Certificates;
using Dbosoft.Rebus.Configuration;
using RabbitMQ.Client;
using Rebus.Config;
using Rebus.RabbitMq;
using Rebus.Transport;

namespace Eryph.Rebus
{
    /// <summary>
    /// Configures the RabbitMQ transport. When a client certificate and CA trust bundle are
    /// supplied (the split runtime with bus mTLS), the connection uses TLS, presents the
    /// component's client certificate and validates the broker certificate against the bundle.
    /// Without them it connects plaintext (e.g. the dev path).
    /// </summary>
    public class RabbitMqRebusTransportConfigurer : IRebusTransportConfigurer
    {
        private readonly X509Certificate2 _clientCertificate;
        private readonly X509Certificate2Collection _caTrustBundle;

        public RabbitMqRebusTransportConfigurer()
        {
        }

        public RabbitMqRebusTransportConfigurer(
            X509Certificate2 clientCertificate,
            X509Certificate2Collection caTrustBundle)
        {
            _clientCertificate = clientCertificate;
            _caTrustBundle = caTrustBundle;
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

        private static string ConnectionString() =>
            Environment.GetEnvironmentVariable("RABBITMQ_CONNECTIONSTRING");

        private void ApplyMutualTls(RabbitMqOptionsBuilder options)
        {
            if (_clientCertificate is null)
                return;

            options.CustomizeConnectionFactory(factory =>
            {
                var connectionFactory = (ConnectionFactory)factory;
                connectionFactory.Ssl = new SslOption
                {
                    Enabled = true,
                    ServerName = connectionFactory.HostName,
                    Certs = [_clientCertificate],
                    // Validate the broker against the distributed CA trust bundle rather than the
                    // machine trust store.
                    CertificateValidationCallback = (_, certificate, _, _) =>
                        certificate is not null && ChainsToTrustedRoot(certificate),
                };
                return connectionFactory;
            });
        }

        private bool ChainsToTrustedRoot(X509Certificate certificate)
        {
            using var serverCertificate = X509CertificateLoader.LoadCertificate(
                certificate.Export(X509ContentType.Cert));
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            foreach (var ca in _caTrustBundle)
                chain.ChainPolicy.CustomTrustStore.Add(ca);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(serverCertificate);
        }

        private void WaitForConnection()
        {
            RabbitMqConnectionCheck.WaitForRabbitMq(new TimeSpan(0, 0, 10)).Wait();
        }
    }
}
