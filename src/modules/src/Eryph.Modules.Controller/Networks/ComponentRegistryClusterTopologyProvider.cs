using Eryph.Core;
using LanguageExt;

namespace Eryph.Modules.Controller.Networks;

/// <summary>
/// Builds the OVN cluster topology from <see cref="IComponentRegistry"/>. Replaces
/// the fixed single-host provider; for one registered host it yields the single
/// local chassis, so behavior is unchanged. The chassis group is the cluster-wide
/// gateway group and stays constant.
/// </summary>
internal sealed class ComponentRegistryClusterTopologyProvider(
    IComponentRegistry componentRegistry)
    : IClusterTopologyProvider
{
    public string ChassisGroupName => EryphConstants.Networking.LocalChassisGroupName;

    public Seq<(string ChassisName, short Priority)> GetChassis() =>
        componentRegistry.GetHostAgents()
            .Map(agent => (agent.ChassisName, agent.ChassisPriority));
}
