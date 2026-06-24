using System;
using System.Net;
using Eryph.ModuleCore.Components;
using Eryph.Modules.AspNetCore.Components;
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
    /// default URLs so no unauthenticated default port is bound. Certificate load and the mutual-TLS
    /// wiring are shared (the component certificate store + <see cref="ComponentTls.ConfigureHttps"/>);
    /// only the bind endpoint is agent-specific.
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

                var store = new FileComponentCertificateStore(
                    certificateDirectory, ComponentCertificateDefaults.RenewalLeadTime);
                var (leaf, chain) = store.LoadServerCertificate();
                var trustedRoots = store.LoadCaTrustBundle();
                if (trustedRoots.Count == 0)
                    throw new InvalidOperationException(
                        "The component CA trust bundle is empty; cannot validate the compute API's client certificate.");

                options.Listen(ip, port, listenOptions =>
                    listenOptions.UseHttps(https =>
                        ComponentTls.ConfigureHttps(https, leaf, chain, trustedRoots)));
            });
        }
    }
}
