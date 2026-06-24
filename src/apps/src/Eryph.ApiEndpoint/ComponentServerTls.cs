using System;
using Eryph.ModuleCore.Components;
using Eryph.Modules.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;

namespace Eryph.ApiEndpoint;

/// <summary>
/// The compute API serves HTTPS with the server-TLS certificate it received at enrollment (issued
/// by the identity server-TLS sub-CA). It is loaded when Kestrel starts — by then enrollment (which
/// blocks in the host filter) has written it to the certificate directory. The certificate load and
/// the Kestrel wiring are shared with the other component listeners (the store loader +
/// <see cref="Eryph.Modules.AspNetCore.Components.ComponentTls"/>), so only the directory lookup is
/// component-specific.
/// </summary>
internal static class ComponentServerTls
{
    public static void Configure(IWebHostBuilder webHostBuilder)
    {
        // The Kestrel options callback runs at server start (after the enrollment host filter has
        // produced the certificate files) and gives access to IConfiguration.
        webHostBuilder.ConfigureKestrel((context, options) =>
        {
            var certificateDirectory = context.Configuration.GetSection("componentMtls")["certificateDirectory"];
            if (string.IsNullOrWhiteSpace(certificateDirectory))
                throw new InvalidOperationException(
                    "componentMtls:certificateDirectory must be set to serve component TLS.");

            var store = new FileComponentCertificateStore(certificateDirectory,
                ComponentCertificateDefaults.RenewalLeadTime);
            var (leaf, chain) = store.LoadServerCertificate();
            options.ConfigureHttpsDefaults(https =>
                ComponentTls.ConfigureHttps(https, leaf, chain));
        });
    }
}
