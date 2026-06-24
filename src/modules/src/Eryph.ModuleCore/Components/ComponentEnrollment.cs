using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
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
        IComponentCertificateStore store,
        ComponentIdentity identity,
        IEndpointResolver endpointResolver,
        ComponentEnrollmentClientOptions options,
        string trustAnchorBundlePath,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        EnsureEnrolledAsync(
                store, identity, endpointResolver, options, trustAnchorBundlePath, loggerFactory, cancellationToken)
            .GetAwaiter().GetResult();

        var clientCertificatePfxPath = store.GetClientCertificatePfxPath()
            ?? throw new InvalidOperationException("Enrollment did not produce a usable client certificate.");

        // The deployment root CA must be installed into the host trust store (a provisioning step)
        // so the broker's server certificate validates; the transport presents the client cert file.
        return new RabbitMqRebusTransportConfigurer(clientCertificatePfxPath);
    }

    /// <summary>
    /// Ensures the store holds a current certificate, (re)issuing one only when it is missing, expired,
    /// or inside its renewal window. Shared by the startup bootstrap (above) and the periodic renewal
    /// service so there is a single enroll/renew code path:
    /// <list type="bullet">
    /// <item>no usable certificate (missing/expired): enroll with the one-time token, blocking and
    /// retrying until the identity service answers (the bus cannot connect without a certificate);</item>
    /// <item>a still-valid but renewal-due certificate: renew by authenticating with that certificate
    /// (mTLS) against the renew endpoint — the token is one-time and cannot be reused — in a single
    /// non-blocking attempt so a healthy component is never blocked.</item>
    /// </list>
    /// </summary>
    public static async Task EnsureEnrolledAsync(
        IComponentCertificateStore store,
        ComponentIdentity identity,
        IEndpointResolver endpointResolver,
        ComponentEnrollmentClientOptions options,
        string trustAnchorBundlePath,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default,
        bool force = false)
    {
        if (!force && store.HasCurrentCertificate())
            return;

        // Present the current client certificate when one exists so a renewal authenticates with mTLS
        // against the renew endpoint; a first enrollment has none yet and authenticates with the token.
        using var clientCertificate = store.LoadClientCertificate();
        using var httpClient = CreateEnrollmentHttpClient(trustAnchorBundlePath, clientCertificate);
        var transport = new HttpEnrollmentTransport(httpClient, endpointResolver);
        var client = new ComponentEnrollmentClient(
            transport, store, identity, options,
            loggerFactory.CreateLogger<ComponentEnrollmentClient>());

        await client.EnsureEnrolledAsync(cancellationToken, force);
    }

    private static HttpClient CreateEnrollmentHttpClient(
        string trustAnchorBundlePath, X509Certificate2? clientCertificate)
    {
        // Trust the identity service's server certificate via the pre-provisioned CA root bundle
        // (placed by the deployment tooling), not the machine trust store — the same single root.
        var trustAnchors = new X509Certificate2Collection();
        if (File.Exists(trustAnchorBundlePath))
            trustAnchors.ImportFromPemFile(trustAnchorBundlePath);

        // Without a trust anchor every TLS handshake to the identity service fails validation, and the
        // enrollment loop would retry forever against an endpoint it can never trust. Fail fast with an
        // actionable error so a missing/empty CA bundle is obvious instead of looking like an outage.
        if (trustAnchors.Count == 0)
            throw new InvalidOperationException(
                $"No CA trust anchors were loaded from '{trustAnchorBundlePath}'. The enrollment file's "
                + "CA certificate must be provisioned there before the component can enroll.");

        // The handler owns the loaded anchors and disposes them with itself; HttpClient disposes the
        // handler (disposeHandler defaults to true), so disposing the HttpClient releases the anchor
        // handles. Otherwise the imported certificates would leak on every enrollment/restart.
        var handler = new EnrollmentHttpClientHandler(trustAnchors);

        // For renewal, present the component's current client certificate so the renew endpoint can
        // authenticate it (mTLS) instead of the spent one-time token. A first enrollment has no
        // certificate and the handler presents none — the token in the request authenticates instead.
        if (clientCertificate is not null)
            handler.ClientCertificates.Add(clientCertificate);

        return new HttpClient(handler);
    }

    // Validates the identity server certificate against the pre-provisioned trust anchors (shared mTLS
    // evaluation: rejects host-name mismatch, builds the chain through the presented intermediates to
    // the root, requires serverAuth) and owns those anchors' native handles.
    private sealed class EnrollmentHttpClientHandler : HttpClientHandler
    {
        private readonly X509Certificate2Collection _trustAnchors;

        public EnrollmentHttpClientHandler(X509Certificate2Collection trustAnchors)
        {
            _trustAnchors = trustAnchors;
            ServerCertificateCustomValidationCallback = (_, certificate, chain, errors) =>
                TrustEvaluation.IsTrustedServerCertificate(certificate, chain, errors, _trustAnchors);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (var anchor in _trustAnchors)
                    anchor.Dispose();
            base.Dispose(disposing);
        }
    }
}
