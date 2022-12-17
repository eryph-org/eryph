using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.ModuleCore;
using Eryph.VmManagement;
using LanguageExt;
using Rebus.Bus;

namespace Eryph.Modules.VmHostAgent.Images;

internal class ImageRequestRegistry : IImageRequestDispatcher
{
    private readonly IBus _bus;
    private readonly IImageRequestBackgroundQueue _queue;
    private readonly IImageProvider _imageProvider;

    public ImageRequestRegistry(IBus bus, IImageRequestBackgroundQueue queue, IImageProvider imageProvider)
    {
        _bus = bus;
        _queue = queue;
        _imageProvider = imageProvider;
    }

    private readonly Atom<HashMap<string, Arr<ListingTask>>> _imageQueue = Prelude.Atom(HashMap<string, 
        Arr<ListingTask>>.Empty);

    public void NewImageRequestTask(IOperationTaskMessage message, string image)
    {
        _imageQueue.Swap(queue => queue.AddOrUpdate(image,
            Some: td => td.Add(new ListingTask(message.OperationId, message.InitiatingTaskId, message.TaskId)),
            None: () =>
            {
                _queue.QueueBackgroundWorkItemAsync(token => ProvideImage(image, token));
                return new Arr<ListingTask>(new[] { new ListingTask(message.OperationId, message.InitiatingTaskId, message.TaskId) });
            }));


    }

    private async ValueTask ProvideImage(string image, CancellationToken cancel)
    {

        try
        {
            var result = await _imageProvider.ProvideImage(
                image,
                (message) => ReportProgress(image, message), cancel);
            await EndRequest(image, result);
        }
        catch (Exception ex)
        {
            await EndRequest(image, new PowershellFailure { Message = ex.Message });
        }


    }


    private record ListingTask(Guid OperationId, Guid InitiatingTaskId, Guid TaskId) : IOperationTaskMessage
    {

    }

    public Task<Unit> ReportProgress(string image, string message)
    {
        return _imageQueue.Value.Find(image).IfSomeAsync(async  listening =>
        {
            foreach (var task in listening)
            {
                await _bus.ProgressMessage(task, message);
            }

            return Unit.Default;
        });
    }

    public Task EndRequest(string image, Either<PowershellFailure, string> result)
    {
        return _imageQueue.Value.Find(image).IfSomeAsync(async listening =>
        {
            _imageQueue.Swap(queue => queue.Remove(image));
            foreach (var task in listening)
            {
                await result.ToAsync()
                    .ToError()
                    .FailOrComplete(_bus, task);

            }
        });
    }
}