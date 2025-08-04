namespace Eryph.Modules.GenePool.Genetics;

internal class GeneBackgroundTaskQueue : BackgroundTaskQueue, IGeneRequestBackgroundQueue
{
    public GeneBackgroundTaskQueue() : base(3)
    {
    }
}