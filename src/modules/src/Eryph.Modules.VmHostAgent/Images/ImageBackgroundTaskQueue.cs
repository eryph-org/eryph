namespace Eryph.Modules.VmHostAgent.Images;

internal class ImageBackgroundTaskQueue : BackgroundTaskQueue, IImageRequestBackgroundQueue
{
    public ImageBackgroundTaskQueue() : base(3)
    {
    }
}