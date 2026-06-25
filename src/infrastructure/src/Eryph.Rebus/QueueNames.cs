namespace Eryph.Rebus;

public class QueueNames
{
    public const string Controllers = "eryph.controller";
    public const string ApiServices = "eryph.api";
    public const string IdentityServices = "eryph.identity";
    public const string VMHostAgent = "eryph.vmhostagent";
    public const string GenePool = "eryph.genepool";
    public const string Network = "eryph.network";

    /// <summary>
    /// Topic on which operation cancellation requests are broadcast to every host
    /// that runs operation tasks. All task-running modules subscribe to it.
    /// </summary>
    public const string OperationCancellation = "broadcast_operation_cancellation";
}
