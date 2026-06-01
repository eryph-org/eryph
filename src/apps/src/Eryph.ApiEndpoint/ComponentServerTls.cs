using System;
using System.IO;
using System.Linq;
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

                // Load the whole PKCS#12: it bundles the leaf (with key) AND the issuing chain, so the
                // chain travels with the leaf in one atomic file. Reading the chain from a separate PEM
                // would let a crash between the two writes leave the leaf without its chain, and clients
                // pinning only the root could not validate the handshake.
                var bundle = X509CertificateLoader.LoadPkcs12Collection(
                    File.ReadAllBytes(pfxPath), password: null, keyStorageFlags: X509KeyStorageFlags.DefaultKeySet);
                var leaf = bundle.OfType<X509Certificate2>().FirstOrDefault(c => c.HasPrivateKey)
                    ?? throw new InvalidOperationException(
                        $"The enrolled server certificate '{pfxPath}' does not contain a private key.");

                var chain = new X509Certificate2Collection();
                foreach (var certificate in bundle)
                    if (!ReferenceEquals(certificate, leaf))
                        chain.Add(certificate);

                options.ConfigureHttpsDefaults(https =>
                {
                    https.ServerCertificate = leaf;
                    https.ServerCertificateChain = chain;
                });
            });
        }
    }
}
