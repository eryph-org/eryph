using System;
using System.Security.Cryptography.X509Certificates;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

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

                var keyService = new WindowsCertificateKeyService();
                var certificateAuthority = new ComponentCertificateAuthority(
                    new WindowsCertificateStoreService(), new WindowsCertificateGenerator(), keyService);
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
