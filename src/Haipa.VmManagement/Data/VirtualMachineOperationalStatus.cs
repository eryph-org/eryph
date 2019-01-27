namespace Haipa.VmManagement.Data
{
    public enum VirtualMachineOperationalStatus
    {
        Ok = 2,
        Degraded = 3,
        PredictiveFailure = 5,
        InService = 11, // 0x0000000B
        Dormant = 15, // 0x0000000F
        SupportingEntityInError = 16, // 0x00000010
        ApplyingSnapshot = 23769, // 0x00005CD9
        CreatingSnapshot = 32768, // 0x00008000
        DeletingSnapshot = 32770, // 0x00008002
        WaitingToStart = 32771, // 0x00008003
        MergingDisks = 32772, // 0x00008004
        ExportingVirtualMachine = 32773, // 0x00008005
        MigratingVirtualMachine = 32774, // 0x00008006
        BackingUpVirtualMachine = 32776, // 0x00008008
        ModifyingUpVirtualMachine = 32777, // 0x00008009
        StorageMigrationPhaseOne = 32778, // 0x0000800A
        StorageMigrationPhaseTwo = 32779, // 0x0000800B
        MigratingPlannedVm = 32780, // 0x0000800C
        CheckingCompatibility = 32781, // 0x0000800D
        ApplicationCriticalState = 32782, // 0x0000800E
        CommunicationTimedOut = 32783, // 0x0000800F
        CommunicationFailed = 32784, // 0x00008010
        NoIommu = 32785, // 0x00008011
        NoIovSupportInNic = 32786, // 0x00008012
        SwitchNotInIovMode = 32787, // 0x00008013
        IovBlockedByPolicy = 32788, // 0x00008014
        IovNoAvailResources = 32789, // 0x00008015
        IovGuestDriversNeeded = 32790, // 0x00008016
        CriticalIoError = 32795, // 0x0000801B
    }
}