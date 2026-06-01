using System;
using System.Security.Cryptography.X509Certificates;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Eryph.Identity
{
    /// <summary>
    /// When component mTLS is enabled, the identity host serves HTTPS with a server certificate it
    /// self-issues from its own server-TLS sub-CA — so the enrollment endpoint is reachable over TLS
    /// that components validate against the CA they pinned out-of-band (the enrollment file). Identity
    /// hosts the CA, so it resolves its own chicken-and-egg by self-issuing, exactly as it self-issues
    /// its bus client certificate.
    /// </summary>
    internal static class IdentityServerTls
    {
        public static void Configure(IWebHostBuilder webHostBuilder)
        {
            webHostBuilder.ConfigureKestrel((context, options) =>
            {
                var mtls = context.Configuration.GetSection("componentMtls");
                if (!bool.TryParse(mtls["enabled"], out var enabled) || !enabled)
                    return;

                var baseUrl = context.Configuration["ERYPH_IDENTITY_BASEURL"] ?? "https://localhost:8080/";
                var dnsName = new Uri(baseUrl).Host;

                // Resolve the platform key/cert backend from DI (same instances the module uses, so this
                // self-issued cert chains to the same CA). Kestrel's ApplicationServices is the
                // cross-wired provider that holds the registrations from IdentityContainerExtensions.
                var services = options.ApplicationServices;
                var keyService = services.GetRequiredService<ICertificateKeyService>();
                var certificateAuthority = new ComponentCertificateAuthority(
                    services.GetRequiredService<ICertificateStoreService>(),
                    services.GetRequiredService<ICertificateGenerator>(),
                    keyService);
                var issued = new CaServerCertificateProvider(keyService, certificateAuthority)
                    .GetServerCertificate(dnsName);

                // Present leaf + the server intermediate: components pin only the root, so the chain
                // must be sent for them to build leaf -> server-intermediate -> root.
                var chain = new X509Certificate2Collection();
                foreach (var certificate in issued.IssuingChain)
                    chain.Add(certificate);

                options.ConfigureHttpsDefaults(https =>
                {
                    https.ServerCertificate = issued.Leaf;
                    https.ServerCertificateChain = chain;
                });
            });
        }
    }
}
