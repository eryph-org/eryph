namespace HyperVPlus.VmManagement.Data
{
    public enum VMIntegrationComponentOperationalStatus
    {
        Ok = 2,
        Degraded = 3,
        Error = 6,
        NonRecoverableError = 7,
        NoContact = 12, // 0x0000000C
        LostCommunication = 13, // 0x0000000D
        ProtocolMismatch = 32775, // 0x00008007
        ApplicationCritical = 32782, // 0x0000800E
        CommunicationTimedOut = 32783, // 0x0000800F
        CommunicationFailed = 32784, // 0x00008010
        Disabled = 32896, // 0x00008080
    }
}