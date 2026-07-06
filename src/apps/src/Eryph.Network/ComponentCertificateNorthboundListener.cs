using System;
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
        // No server certificate: FAIL the apply rather than return a chassis-only plan. The realizer
        // reconciles the northbound connection/SSL tables, so applying a plan without the listener would
        // remove an existing pssl:6641 listener — the very clobber this design prevents. Throwing aborts
        // the apply before the plan reaches the database, so the current listener is left intact; the
        // configuration is re-applied on the next topology change once enrollment is restored.
        var pem = certificateStore.ReadServerCertificatePem()
                  ?? throw new InvalidOperationException(
                      "The component server certificate (PEM) is not available, so the OVN northbound SSL "
                      + "listener cannot be configured. Check the component enrollment.");

        return plan
            .SetNorthboundSsl(pem.PrivateKeyPem, pem.CertificatePem, pem.CaBundlePem)
            .AddNorthboundConnection(OvnRemoteEndpoints.NorthboundPort, true);
    }
}
