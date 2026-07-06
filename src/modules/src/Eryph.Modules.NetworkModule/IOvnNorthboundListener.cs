using Dbosoft.OVN;

namespace Eryph.Modules.Network;

/// <summary>
/// Contributes the northbound SSL listener (the connection target and SSL material) to the OVN
/// cluster plan that <see cref="OvnClusterConfigRealizer"/> applies to the network component's local
/// northbound database. It is host-supplied: the split runtime appends a <c>pssl:6641</c> listener
/// backed by the enrolled component certificate, while eryph-zero appends none (its northbound
/// database is a local pipe with no SSL and no component certificate). Applying the listener in the
/// SAME plan as the gateway chassis keeps the network module the single writer of the northbound
/// database, so the reconciling realizer never removes a listener that a chassis-only plan omits.
/// </summary>
public interface IOvnNorthboundListener
{
    ClusterPlan Configure(ClusterPlan plan);
}
