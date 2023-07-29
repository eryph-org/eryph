using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Images.Commands;
using Eryph.VmManagement;
using LanguageExt;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Eryph.Modules.VmHostAgent.Images;

internal class ImageRequestRegistry : IImageRequestDispatcher
{
    //private readonly ITaskMessaging _messaging;
    private readonly IImageRequestBackgroundQueue _queue;
    private readonly Container _container;
    private readonly IImageProvider _imageProvider;

    private record ListingTask(Guid OperationId, Guid InitiatingTaskId, Guid TaskId) : IOperationTaskMessage;


    public ImageRequestRegistry(IImageRequestBackgroundQueue queue, Container container,  IImageProvider imageProvider)
    {
        _queue = queue;
        _container = container;
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
        await using var scope = AsyncScopedLifestyle.BeginScope(_container);

        var taskMessaging = scope.GetInstance<ITaskMessaging>();
        try
        {
            var result = await _imageProvider.ProvideImage(
                image,
                (message) => ReportProgress(taskMessaging, image, message), cancel);
            await EndRequest(taskMessaging, image, result);
        }
        catch (Exception ex)
        {
            await EndRequest(taskMessaging, image, new PowershellFailure { Message = ex.Message });
        }


    }

    public Task<Unit> ReportProgress(ITaskMessaging taskMessaging, string image, string message)
    {
        return _imageQueue.Value.Find(image).IfSomeAsync(async  listening =>
        {
            foreach (var task in listening)
            {
                await taskMessaging.ProgressMessage(task, message);
            }

            return Unit.Default;
        });
    }

    public Task EndRequest(ITaskMessaging taskMessaging, string image, Either<PowershellFailure, PrepareVirtualMachineImageResponse> result)
    {
        return _imageQueue.Value.Find(image).IfSomeAsync(async listening =>
        {
            _imageQueue.Swap(queue => queue.Remove(image));
            foreach (var task in listening)
            {
                await result.ToAsync()
                    .ToError()
                    .FailOrComplete(taskMessaging, task);

            }
        });
    }
}