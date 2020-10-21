namespace Haipa.VmManagement.Data
{
    public enum VMNetworkAdapterOperationalStatus
    {
        Unknown = 0,
        Other = 1,
        Ok = 2,
        Degraded = 3,
        Stressed = 4,
        PredictiveFailure = 5,
        Error = 6,
        NonRecoverableError = 7,
        Starting = 8,
        Stopping = 9,
        Stopped = 10, // 0x0000000A
        InService = 11, // 0x0000000B
        NoContact = 12, // 0x0000000C
        LostCommunication = 13, // 0x0000000D
        Aborted = 14, // 0x0000000E
        Dormant = 15, // 0x0000000F
        SupportingEntity = 16, // 0x00000010
        Completed = 17, // 0x00000011
        PowerMode = 18, // 0x00000012
        ProtocolVersion = 32775, // 0x00008007
    }
}