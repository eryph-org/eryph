using System;
using System.Security.Cryptography.X509Certificates;
using Eryph.Modules.AspNetCore.Components;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Eryph.Identity;

/// <summary>
/// The identity host serves HTTPS with a server certificate it self-issues from its own server-TLS
/// sub-CA — so the enrollment endpoint is reachable over TLS that components validate against the CA
/// they pinned out-of-band (the enrollment file). Identity hosts the CA, so it resolves its own
/// chicken-and-egg by self-issuing, exactly as it self-issues its bus client certificate. The Kestrel
/// wiring itself is shared with the other component listeners via
/// <see cref="ComponentTls.ConfigureHttps"/> — only the self-issued certificate source is specific here.
/// </summary>
internal static class IdentityServerTls
{
    public static void Configure(IWebHostBuilder webHostBuilder)
    {
        webHostBuilder.ConfigureKestrel((context, options) =>
        {
            // Require an explicit base URL: the host name is baked into the self-issued server
            // certificate, so falling back to "localhost" would silently issue a certificate
            // components can never validate against the address they connect to.
            var baseUrl = context.Configuration["ERYPH_IDENTITY_BASEURL"];
            if (string.IsNullOrWhiteSpace(baseUrl)
                || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
                throw new InvalidOperationException(
                    "ERYPH_IDENTITY_BASEURL must be set to an absolute URL; it is required to issue the "
                    + "identity server certificate for the correct host name.");
            var dnsName = baseUri.Host;

            // Build the platform key/cert backend directly (the listener runs outside the module
            // container, so it cannot resolve these from DI). PkiOptions.CreateServices is the same
            // backend selection the module uses, pointed at the same store, so this self-issued cert
            // chains to the same CA.
            var (keyService, storeService, generator) = PkiOptions.CreateServices();
            var certificateAuthority = new ComponentCertificateAuthority(storeService, generator, keyService);
            var issued = new CaServerCertificateProvider(keyService, certificateAuthority)
                .GetServerCertificate(dnsName);

            var chain = new X509Certificate2Collection();
            foreach (var certificate in issued.IssuingChain)
                chain.Add(certificate);

            options.ConfigureHttpsDefaults(https =>
            {
                ComponentTls.ConfigureHttps(https, issued.Leaf, chain);
                // Allow (but do not require) a client certificate: token-based enrollment must work
                // without one, while the renewal endpoint authenticates with the component's current
                // certificate. Kestrel must not reject it against the OS trust store — the renewal
                // service validates it against the component CA.
                https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
                https.AllowAnyClientCertificate();
            });
        });
    }
}
