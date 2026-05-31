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
        ILoggerFactory loggerFactory)
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
            // client retries through the identity service still starting.
            client.EnsureEnrolledAsync(CancellationToken.None).GetAwaiter().GetResult();
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
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate is not null && ChainsToTrustedRoot(certificate, trustAnchors),
        };

        return new HttpClient(handler);
    }

    private static bool ChainsToTrustedRoot(X509Certificate2 certificate, X509Certificate2Collection roots)
    {
        if (roots.Count == 0)
            return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(roots);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(certificate);
    }
}
