using LanguageExt;

namespace Eryph.Modules.Controller.Networks;

/// <summary>
/// Provides the OVN cluster topology that the controller realizes against
/// the northbound database before applying network plans.
/// </summary>
/// <remarks>
/// The single-host implementation returns a fixed chassis list. When host
/// enrollment lands, an enrollment-backed implementation will read the
/// registered hosts from the state DB without changing the call sites.
/// </remarks>
public interface IClusterTopologyProvider
{
    string ChassisGroupName { get; }

    Seq<(string ChassisName, short Priority)> GetChassis();
}
