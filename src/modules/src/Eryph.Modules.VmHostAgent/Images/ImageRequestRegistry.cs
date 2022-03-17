using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Operations.Events;
using Eryph.VmManagement;
using LanguageExt;
using Rebus.Bus;
using Rebus.Transport;

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

    private readonly Atom<HashMap<string, Arr<ListingTask>>> _imageQueue = Prelude.Atom(HashMap<string, Arr<ListingTask>>.Empty);

    public void NewImageRequestTask(Guid operationId, Guid taskId, string image)
    {
        _imageQueue.Swap(queue => queue.AddOrUpdate(image,
            Some: td => td.Add(new ListingTask(operationId, taskId)),
            None: () =>
            {
                _queue.QueueBackgroundWorkItemAsync(token => ProvideImage(image, token));
                return new Arr<ListingTask>(new[] { new ListingTask(operationId, taskId) });
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


    private record ListingTask(Guid OperationId, Guid TaskId)
    {
        public readonly Guid OperationId = OperationId;
        public readonly Guid TaskId = TaskId;
    }

    public Task<Unit> ReportProgress(string image, string message)
    {
        return _imageQueue.Value.Find(image).IfSomeAsync(async  listening =>
        {
            foreach (var (operationId, taskId) in listening)
            {
                await ProgressMessage(operationId, taskId, message);
            }

            return Unit.Default;
        });
    }

    private async Task ProgressMessage(Guid operationId, Guid taskId, string message)
    {
        using var scope = new RebusTransactionScope();
        await _bus.Publish(new OperationTaskProgressEvent
        {
            Id = Guid.NewGuid(),
            OperationId = operationId,
            TaskId = taskId,
            Message = message,
            Timestamp = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);

        // commit it like this
        await scope.CompleteAsync().ConfigureAwait(false);
    }

    public Task EndRequest(string image, Either<PowershellFailure, string> result)
    {
        return _imageQueue.Value.Find(image).IfSomeAsync(async listening =>
        {
            _imageQueue.Swap(queue => queue.Remove(image));
            foreach (var (operationId, taskId) in listening)
            {
                await result.ToAsync()
                    .MatchAsync(r =>
                            _bus.Publish(OperationTaskStatusEvent.Completed(operationId, taskId, r)),
                        l =>
                        {
                            return _bus.Publish(OperationTaskStatusEvent.Failed(operationId, taskId,
                                new ErrorData { ErrorMessage = l.Message }));
                        });

            }
        });
    }
}