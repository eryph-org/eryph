using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using Rebus.Retry.Simple;

namespace Eryph.ModuleCore;

/// <summary>
/// This handler ensures that operation tasks which repeatedly failed to process
/// are reported as failed.
/// </summary>
/// <remarks>
/// This handler is only required when the module does not use
/// <see cref="SimpleInjectorExtensions.AddRebusOperationsHandlers{TOpManager,TTaskManager}"/>.
/// </remarks>
public class FailedOperationTaskHandler<T>(
    ILogger logger,
    ITaskMessaging taskMessaging)
    : IHandleMessages<IFailed<OperationTask<T>>>
    where T : class, new()
{
    public Task Handle(IFailed<OperationTask<T>> message)
    {
        logger.LogError("Task {taskId} failed with message: {failedMessage}",
            message.Message.TaskId, message.ErrorDescription
        );

        return taskMessaging.FailTask(message.Message, message.ErrorDescription);
    }
}
