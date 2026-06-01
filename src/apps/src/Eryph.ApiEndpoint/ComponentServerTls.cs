using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;

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
            if (!bool.TryParse(Environment.GetEnvironmentVariable("componentMtls__enabled"), out var enabled) || !enabled)
                return;

            var certificateDirectory = Environment.GetEnvironmentVariable("componentMtls__certificateDirectory");
            if (string.IsNullOrWhiteSpace(certificateDirectory))
                return;

            // The Kestrel options callback runs at server start, after the enrollment host filter has
            // produced the certificate files.
            webHostBuilder.ConfigureKestrel(options =>
            {
                var pfxPath = Path.Combine(certificateDirectory, "server.pfx");
                if (!File.Exists(pfxPath))
                    return;

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
