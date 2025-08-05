using System.Text.Json.Serialization;

namespace Eryph.StateDb.Model;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CatletStatus
{
    /// <summary>
    /// The status of the catlet is unknown.
    /// </summary>
    Unknown = 0,
    
    /// <summary>
    /// The catlet is stopped.
    /// </summary>
    Stopped = 1,

    /// <summary>
    /// The catlet is running.
    /// </summary>
    Running = 2,

    /// <summary>
    /// The catlet is currently performing a state transition
    /// (e.g. starting or stopping).
    /// </summary>
    Pending = 3,

    /// <summary>
    /// The catlet is in an error state. This does not necessarily
    /// mean that the catlet is not running but the hypervisor
    /// considers the catlet not fully functional.
    /// </summary>
    Error = 4,

    /// <summary>
    /// The catlet is managed by eryph, but it was not found when
    /// the Hyper-V host was inventoried.
    /// </summary>
    Missing = 5,
}
