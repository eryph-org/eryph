namespace Eryph.Modules.VmHostAgent.Genetics;

internal class GeneBackgroundTaskQueue : BackgroundTaskQueue, IGeneRequestBackgroundQueue
{
    public GeneBackgroundTaskQueue() : base(3)
    {
    }
}