namespace Haipa.VmManagement.Data
{
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
        Paused
    }
}