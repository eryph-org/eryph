using Eryph.Modules.VmHostAgent.Images;

namespace Eryph.Modules.VmHostAgent;

internal class ImageBackgroundTaskQueue : BackgroundTaskQueue, IImageRequestBackgroundQueue
{
    public ImageBackgroundTaskQueue() : base(3)
    {
    }
}