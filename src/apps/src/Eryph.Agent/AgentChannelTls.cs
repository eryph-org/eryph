using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Eryph.Modules.HostAgent.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;

namespace Eryph.Agent
{
    /// <summary>
    /// When the channel is enabled, binds the agent's Kestrel to a single HTTPS endpoint on the internal
    /// address serving the <c>/v1/channels/{token}</c> WebSocket. It presents the enrolled component
    /// server certificate and requires a client certificate signed by the component client CA, so only a
    /// valid component (the compute API) can connect. The explicit <c>Listen</c> overrides the host's
    /// default URLs so no unauthenticated default port is bound.
    /// </summary>
    internal static class AgentChannelTls
    {
        public static void Configure(IWebHostBuilder webHostBuilder)
        {
            webHostBuilder.ConfigureKestrel((context, options) =>
            {
                var channel = context.Configuration.GetSection(ChannelListenerOptions.SectionName);
                if (!bool.TryParse(channel["enabled"], out var enabled) || !enabled)
                    return;

                var certificateDirectory =
                    context.Configuration.GetSection("componentMtls")["certificateDirectory"];
                if (string.IsNullOrWhiteSpace(certificateDirectory))
                    throw new InvalidOperationException(
                        "The EGS channel listener is enabled but componentMtls:certificateDirectory is not set; "
                        + "the listener needs the enrolled server certificate and the component CA trust bundle.");

                var bindAddress = channel["bindAddress"];
                if (string.IsNullOrWhiteSpace(bindAddress))
                    bindAddress = "127.0.0.1";
                if (!IPAddress.TryParse(bindAddress, out var ip))
                    throw new InvalidOperationException(
                        $"The EGS channel listener bind address '{bindAddress}' is not a valid IP address.");

                var port = int.TryParse(channel["port"], out var configuredPort) ? configuredPort : 9700;

                var (serverCertificate, serverCertificateChain) = LoadServerCertificate(certificateDirectory);
                var trustedRoots = LoadTrustedRoots(certificateDirectory);

                options.Listen(ip, port, listenOptions =>
                {
                    listenOptions.UseHttps(https =>
                    {
                        https.ServerCertificate = serverCertificate;
                        // Present the issuing intermediate alongside the leaf so the compute API can build
                        // leaf -> intermediate -> root against the root it pins (its validator chains via
                        // the presented intermediates, exactly like the bus transport).
                        https.ServerCertificateChain = serverCertificateChain;
                        // Require a client certificate; the per-request validation against the component
                        // CA happens below so we bind to the deployment PKI rather than the OS trust store.
                        https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                        https.ClientCertificateValidation = (clientCertificate, chain, _) =>
                            ComponentClientCertificateValidator.IsTrustedComponentClient(
                                clientCertificate, chain, trustedRoots);
                    });
                });
            });
        }

        private static (X509Certificate2 Leaf, X509Certificate2Collection Chain) LoadServerCertificate(
            string certificateDirectory)
        {
            // Mirror ComponentServerTls (the compute API's loader): server.pfx bundles the leaf with its
            // private key plus the issuing chain in one atomic file. The leaf is presented together with
            // its intermediate so the peer can build a chain to the root it pins.
            var pfxPath = Path.Combine(certificateDirectory, "server.pfx");
            if (!File.Exists(pfxPath))
                throw new InvalidOperationException(
                    $"The EGS channel listener is enabled but the enrolled server certificate '{pfxPath}' is "
                    + "missing; refusing to start without TLS.");

            var bundle = X509CertificateLoader.LoadPkcs12Collection(
                File.ReadAllBytes(pfxPath), password: null, keyStorageFlags: X509KeyStorageFlags.DefaultKeySet);
            var leaf = bundle.OfType<X509Certificate2>().FirstOrDefault(c => c.HasPrivateKey)
                ?? throw new InvalidOperationException(
                    $"The enrolled server certificate '{pfxPath}' does not contain a private key.");

            var chain = new X509Certificate2Collection();
            foreach (var certificate in bundle)
                if (!ReferenceEquals(certificate, leaf))
                    chain.Add(certificate);

            return (leaf, chain);
        }

        private static X509Certificate2Collection LoadTrustedRoots(string certificateDirectory)
        {
            // The component CA trust bundle written at enrollment (ca-bundle.pem) holds the single root
            // that anchors both the server-TLS and client intermediates; validating the incoming client
            // certificate against it is the same trust anchor the bus transport and the compute-API
            // dialer use — no divergent trust path.
            var bundlePath = Path.Combine(certificateDirectory, "ca-bundle.pem");
            if (!File.Exists(bundlePath))
                throw new InvalidOperationException(
                    $"The EGS channel listener is enabled but the component CA trust bundle '{bundlePath}' is "
                    + "missing; cannot validate the compute API's client certificate.");

            var roots = new X509Certificate2Collection();
            roots.ImportFromPemFile(bundlePath);
            if (roots.Count == 0)
                throw new InvalidOperationException(
                    $"The component CA trust bundle '{bundlePath}' contains no certificates.");

            return roots;
        }
    }
}
