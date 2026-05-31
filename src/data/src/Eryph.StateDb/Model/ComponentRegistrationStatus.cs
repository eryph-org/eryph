namespace Eryph.StateDb.Model;

/// <summary>
/// Liveness state of a registered component, maintained by the controller from
/// registration and heartbeats.
/// </summary>
public enum ComponentRegistrationStatus
{
    Registering,
    Active,
    Stale,
    Dead,
}
