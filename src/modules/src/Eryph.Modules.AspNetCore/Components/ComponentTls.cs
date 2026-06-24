using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Eryph.Modules.AspNetCore.Components;

/// <summary>
/// Shared Kestrel HTTPS wiring for a split-runtime component listener. Presents the component's
/// server-TLS leaf together with its issuing chain, so a peer that pins only the component root can
/// build leaf -&gt; intermediate -&gt; root. When a component CA trust bundle is supplied the listener
/// also requires and validates a client certificate (mutual TLS). The certificate material is resolved
/// by the caller — the enrolled component certificate store, or identity's self-issued server
/// certificate — so every component listener applies one consistent policy regardless of source.
/// </summary>
public static class ComponentTls
{
    public static void ConfigureHttps(
        HttpsConnectionAdapterOptions https,
        X509Certificate2 serverCertificate,
        X509Certificate2Collection issuingChain,
        X509Certificate2Collection? requireClientCertFrom = null)
    {
        https.ServerCertificate = serverCertificate;
        https.ServerCertificateChain = issuingChain;

        if (requireClientCertFrom is null)
            return;

        // Mutual TLS: require a client certificate and validate it against the deployment's component
        // CA (not the OS trust store), so only an enrolled component may connect.
        https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
        https.ClientCertificateValidation = (clientCertificate, clientChain, _) =>
            ComponentClientCertificateValidator.IsTrustedComponentClient(
                clientCertificate, clientChain, requireClientCertFrom);
    }
}
