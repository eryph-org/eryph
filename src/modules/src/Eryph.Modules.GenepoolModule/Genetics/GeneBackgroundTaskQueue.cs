namespace Eryph.Modules.Genepool.Genetics;

internal class GeneBackgroundTaskQueue : BackgroundTaskQueue, IGeneRequestBackgroundQueue
{
    public GeneBackgroundTaskQueue() : base(3)
    {
    }
}