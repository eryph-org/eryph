using Dbosoft.OVN;
using Eryph.Core;
using Eryph.ModuleCore.Components;
using Eryph.Modules.Network;

namespace Eryph.Network;

/// <summary>
/// Contributes the northbound <c>pssl:6641</c> SSL listener to the OVN cluster plan, backed by the
/// enrolled component server certificate. Only the split runtime exposes the northbound database to
/// remote clients (the controller dials it), so only this host appends a listener; eryph-zero adds
/// none. The listener is a TLS <em>server</em> endpoint, so it presents the server certificate
/// (serverAuth EKU) — a client validating the peer rejects the clientAuth-only leaf.
/// </summary>
internal sealed class ComponentCertificateNorthboundListener(
    IComponentCertificateStore certificateStore)
    : IOvnNorthboundListener
{
    public ClusterPlan Configure(ClusterPlan plan)
    {
        var pem = certificateStore.ReadServerCertificatePem();
        if (pem is null)
            // No server certificate: cannot open an SSL listener. Leave the plan unchanged rather than
            // advertise a listener that will not accept connections. The southbound endpoint service
            // fails the process fast on the same missing material, so this is a defensive no-op.
            return plan;

        return plan
            .SetNorthboundSsl(pem.PrivateKeyPem, pem.CertificatePem, pem.CaBundlePem)
            .AddNorthboundConnection(OvnRemoteEndpoints.NorthboundPort, true);
    }
}
