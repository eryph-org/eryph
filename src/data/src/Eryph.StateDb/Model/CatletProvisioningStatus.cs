using System.Text.Json.Serialization;

namespace Eryph.StateDb.Model;

/// <summary>
/// The provisioning status of a catlet as observed via guest-services
/// (the <c>eryph.provisioning.state</c> KVP value, set natively by egs on
/// Windows and mirrored from cloud-init on Linux).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CatletProvisioningStatus
{
    /// <summary>
    /// The provisioning status is unknown. This is the initial value for a
    /// freshly created catlet and the value when guest-services has not
    /// reported a provisioning state (yet).
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Provisioning has started but no stage is running yet.
    /// </summary>
    Started = 1,

    /// <summary>
    /// Provisioning is running.
    /// </summary>
    Running = 2,

    /// <summary>
    /// Provisioning is waiting for a reboot to continue.
    /// </summary>
    RebootPending = 3,

    /// <summary>
    /// Provisioning has completed successfully.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// Provisioning has failed.
    /// </summary>
    Failed = 5,
}
