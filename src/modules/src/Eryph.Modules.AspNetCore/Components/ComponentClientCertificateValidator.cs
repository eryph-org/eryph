#nullable enable
using System.Security.Cryptography.X509Certificates;

namespace Eryph.Modules.AspNetCore.Components;

/// <summary>
/// Validates a client certificate presented to a component's mutual-TLS listener. The connection is
/// accepted only when the certificate chains to one of the deployment's trusted component CA roots
/// (the same root that anchors the component client CA) and carries the clientAuth EKU — so only a
/// valid eryph component can connect. Shared by every component listener that requires mutual TLS
/// (e.g. the agent's EGS channel), so the trust check is defined once.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Eryph.Rebus.TrustEvaluation"/>, but for the client direction (clientAuth EKU)
/// and against the component CA trust bundle written at enrollment (<c>ca-bundle.pem</c>, loaded via
/// <see cref="ModuleCore.Components.IComponentCertificateStore.LoadCaTrustBundle"/>). The single
/// component root anchors both the server-TLS and client intermediates, so the same trust bundle
/// validates the peer here.
/// </remarks>
public static class ComponentClientCertificateValidator
{
    private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2"; // id-kp-clientAuth

    public static bool IsTrustedComponentClient(
        X509Certificate2? clientCertificate,
        X509Chain? presentedChain,
        X509Certificate2Collection trustedRoots)
    {
        if (clientCertificate is null || trustedRoots is null || trustedRoots.Count == 0)
            return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(trustedRoots);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        // The peer presents leaf + client intermediate in the handshake; Kestrel surfaces only the
        // leaf to this callback, so the intermediate must come from the presented chain. The trust
        // bundle holds the root anchor only, so without the presented intermediates the fresh chain
        // has no source to build leaf -> intermediate -> root.
        if (presentedChain is not null)
        {
            foreach (var element in presentedChain.ChainElements)
                chain.ChainPolicy.ExtraStore.Add(element.Certificate);
        }

        return chain.Build(clientCertificate) && HasClientAuthEku(clientCertificate);
    }

    private static bool HasClientAuthEku(X509Certificate2 certificate)
    {
        foreach (var extension in certificate.Extensions)
        {
            if (extension is X509EnhancedKeyUsageExtension eku)
            {
                foreach (var oid in eku.EnhancedKeyUsages)
                {
                    if (oid.Value == ClientAuthOid)
                        return true;
                }
            }
        }

        return false;
    }
}
