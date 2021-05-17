namespace Haipa.VmManagement.Data
{
    public enum VMReplicationState
    {
        Disabled,
        ReadyForInitialReplication,
        InitialReplicationInProgress,
        WaitingForInitialReplication,
        Replicating,
        PreparedForFailover,
        FailedOverWaitingCompletion,
        FailedOver,
        Suspended,
        Error,
        WaitingForStartResynchronize,
        ResynchronizeSuspended,
        RecoveryInProgress,
        FailbackInProgress,
        FailbackComplete,
        WaitingForUpdateCompletion,
        UpdateError,
        WaitingForRepurposeCompletion,
        PreparedForSyncReplication,
        PreparedForGroupReverseReplication,
        FiredrillInProgress
    }
}