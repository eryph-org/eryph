using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Eryph.Rebus;
using Microsoft.Extensions.Logging;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Host-side helper that brings a split-runtime component to a state where it can join the bus over
/// mTLS: it ensures the component is enrolled (blocking, retry-tolerant — see
/// <see cref="ComponentEnrollmentClient"/>), then returns a RabbitMQ transport configurer wired
/// with the component's client certificate and CA trust bundle. Call this from a host filter before
/// the module configures its bus, so the certificate is available when the transport connects.
/// </summary>
public static class ComponentEnrollment
{
    public static RabbitMqRebusTransportConfigurer EnsureEnrolledTransport(
        ComponentIdentity identity,
        IEndpointResolver endpointResolver,
        ComponentEnrollmentClientOptions options,
        string certificateDirectory,
        string trustAnchorBundlePath,
        TimeSpan renewalLeadTime,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var store = new FileComponentCertificateStore(certificateDirectory, renewalLeadTime);

        if (!store.HasValidCertificate())
        {
            using var httpClient = CreateEnrollmentHttpClient(trustAnchorBundlePath);
            var transport = new HttpEnrollmentTransport(httpClient, endpointResolver);
            var client = new ComponentEnrollmentClient(
                transport, store, identity, options,
                loggerFactory.CreateLogger<ComponentEnrollmentClient>());

            // Block here until enrolled: the bus cannot connect without the certificate, and the
            // client retries through the identity service still starting. The token lets the host
            // abort a stuck startup cleanly.
            client.EnsureEnrolledAsync(cancellationToken).GetAwaiter().GetResult();
        }

        var clientCertificate = store.LoadClientCertificate()
            ?? throw new InvalidOperationException("Enrollment did not produce a usable client certificate.");

        return new RabbitMqRebusTransportConfigurer(clientCertificate, store.LoadCaTrustBundle());
    }

    private static HttpClient CreateEnrollmentHttpClient(string trustAnchorBundlePath)
    {
        // Trust the identity service's server certificate via the pre-provisioned CA root bundle
        // (placed by the deployment tooling), not the machine trust store — the same single root.
        var trustAnchors = new X509Certificate2Collection();
        if (File.Exists(trustAnchorBundlePath))
            trustAnchors.ImportFromPemFile(trustAnchorBundlePath);

        var handler = new HttpClientHandler
        {
            // Reuse the shared mTLS validation: rejects host-name mismatch, builds the chain through
            // the presented intermediates to the pre-provisioned root, and requires serverAuth.
            ServerCertificateCustomValidationCallback = (_, certificate, chain, errors) =>
                TrustEvaluation.IsTrustedServerCertificate(certificate, chain, errors, trustAnchors),
        };

        return new HttpClient(handler);
    }
}
