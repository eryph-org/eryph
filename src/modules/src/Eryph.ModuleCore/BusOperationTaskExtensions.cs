using System;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Bus;
using Rebus.Transport;

namespace Eryph.ModuleCore;

#pragma warning restore 1998
public static class BusOperationTaskExtensions
{
    public static Task FailTask(this IBus bus, IOperationTaskMessage message, string errorMessage)
    {
        return FailTask(bus, message, new ErrorData { ErrorMessage = errorMessage });
    }

    public static Task FailTask(this IBus bus, IOperationTaskMessage message, Error error)
    {
        return FailTask(bus, message, new ErrorData { ErrorMessage = error.Message });
    }

    public static Task FailTask(this IBus bus, IOperationTaskMessage message, ErrorData error)
    {
        return bus.Publish(
            OperationTaskStatusEvent.Failed(
                message.OperationId, message.InitiatingTaskId,
                message.TaskId, error));
    }


    public static Task CompleteTask(this IBus bus, IOperationTaskMessage message)
    {
        return bus.Publish(
            OperationTaskStatusEvent.Completed(
                message.OperationId, message.InitiatingTaskId, message.TaskId));
    }

    public static Task CompleteTask(this IBus bus, IOperationTaskMessage message, object responseMessage)
    {
        return bus.Publish(
            OperationTaskStatusEvent.Completed(
                message.OperationId, message.InitiatingTaskId, message.TaskId, responseMessage));
    }

    public static Task<Unit> FailOrComplete<TRet>(
       this EitherAsync<Error, TRet> either, IBus bus, IOperationTaskMessage message)
        where TRet: notnull
    {
        return either.MatchAsync(
            LeftAsync: l => bus.FailTask(message, l),
            RightAsync: ret => ret is Unit 
                ? bus.CompleteTask(message) 
                : bus.CompleteTask(message, ret));

    }


    public static async Task ProgressMessage(this IBus bus, IOperationTaskMessage message, string progressMessage)
    {
        using var scope = new RebusTransactionScope();


        await bus.Publish(new OperationTaskProgressEvent
        {
            Id = Guid.NewGuid(),
            OperationId = message.OperationId,
            TaskId = message.TaskId,
            Message = progressMessage,
            Timestamp = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);

        // commit it like this
        await scope.CompleteAsync().ConfigureAwait(false);
    }
}