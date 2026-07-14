namespace Eryph.Resources.Machines;

/// <summary>
/// The provisioning status of a catlet as reported by guest-services via the
/// <c>eryph.provisioning.state</c> KVP value.
/// </summary>
public enum ProvisioningStatus
{
    Unknown = 0,
    Started = 1,
    Running = 2,
    RebootPending = 3,
    Completed = 4,
    Failed = 5,
}
