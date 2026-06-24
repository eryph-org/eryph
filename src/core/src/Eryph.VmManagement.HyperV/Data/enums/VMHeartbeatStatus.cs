namespace Eryph.VmManagement.Data.enums;

public enum VMHeartbeatStatus
{
    Unknown,
    Disabled,
    NoContact,
    Error,
    LostCommunication,
    OkApplicationsUnknown,
    OkApplicationsHealthy,
    OkApplicationsCritical,
    Paused,
}
