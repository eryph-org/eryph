using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Eryph.ApiEndpoint
{
    /// <summary>
    /// When component mTLS is enabled, the compute API serves HTTPS with the server-TLS certificate it
    /// received at enrollment (issued by the identity server-TLS sub-CA). The certificate is loaded
    /// when Kestrel starts — by then enrollment (which blocks in the host filter) has written it to the
    /// certificate directory. It presents leaf + server intermediate so clients build a chain to the
    /// root they trust.
    /// </summary>
    internal static class ComponentServerTls
    {
        public static void Configure(IWebHostBuilder webHostBuilder)
        {
            // The Kestrel options callback runs at server start (after the enrollment host filter has
            // produced the certificate files) and gives access to IConfiguration.
            webHostBuilder.ConfigureKestrel((context, options) =>
            {
                var mtls = context.Configuration.GetSection("componentMtls");
                if (!bool.TryParse(mtls["enabled"], out var enabled) || !enabled)
                    return;

                var certificateDirectory = mtls["certificateDirectory"];
                if (string.IsNullOrWhiteSpace(certificateDirectory))
                    throw new InvalidOperationException(
                        "componentMtls is enabled but componentMtls:certificateDirectory is not set.");

                var pfxPath = Path.Combine(certificateDirectory, "server.pfx");
                if (!File.Exists(pfxPath))
                    // Fail closed: mTLS was requested but the enrolled server certificate is absent —
                    // do not silently fall back to unauthenticated HTTP.
                    throw new InvalidOperationException(
                        $"componentMtls is enabled but the enrolled server certificate '{pfxPath}' is missing; "
                        + "refusing to start without TLS.");

                var leaf = X509CertificateLoader.LoadPkcs12(
                    File.ReadAllBytes(pfxPath), password: null, keyStorageFlags: X509KeyStorageFlags.DefaultKeySet);

                var chain = new X509Certificate2Collection();
                var chainPath = Path.Combine(certificateDirectory, "server-chain.pem");
                if (File.Exists(chainPath))
                    chain.ImportFromPemFile(chainPath);

                options.ConfigureHttpsDefaults(https =>
                {
                    https.ServerCertificate = leaf;
                    https.ServerCertificateChain = chain;
                });
            });
        }
    }
}
