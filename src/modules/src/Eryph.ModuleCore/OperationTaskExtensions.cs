using System;
using System.Threading.Tasks;
using Dbosoft.Functional;
using Eryph.Core;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

// ReSharper disable once CheckNamespace
namespace Dbosoft.Rebus.Operations;

#pragma warning restore 1998
public static class OperationTaskExtensions
{
    public static async Task<Unit> FailOrComplete<TRet>(
        this Aff<TRet> aff,
        ITaskMessaging messaging,
        IOperationTaskMessage message)
        where TRet : notnull
    {
        var result = await aff.Run();
        await result.FailOrComplete(messaging, message);
        return unit;
    }

    public static Task<Unit> FailOrComplete<TRet>(
        this Fin<TRet> fin,
        ITaskMessaging messaging,
        IOperationTaskMessage message)
        where TRet : notnull =>
        fin.Match(
            Succ: ret => ret is Unit
                ? messaging.CompleteTask(message).ToUnit()
                : messaging.CompleteTask(message, ret).ToUnit(),
            Fail: error => messaging.FailTask(message, error).ToUnit());


    public static Task<Unit> FailOrComplete<TRet>(
        this EitherAsync<Error, TRet> either,
        ITaskMessaging messaging,
        IOperationTaskMessage message)
        where TRet: notnull
    {
        return either.MatchAsync(
            LeftAsync: l => messaging.FailTask(message, l),
            RightAsync: ret => ret is Unit 
                ? messaging.CompleteTask(message) 
                : messaging.CompleteTask(message, ret));
    }

    public static Task<Unit> FailOrContinue(
        this EitherAsync<Error, Unit> either,
        ITaskMessaging messaging,
        IOperationTaskMessage message)
    {
        return either.MatchAsync(
            LeftAsync: l => messaging.FailTask(message, l),
            RightAsync: _ => Task.FromResult(Prelude.unit));
    }

    public static Task FailTask(
        this ITaskMessaging messaging,
        IOperationTaskMessage message,
        Error error)
    {
        return messaging.FailTask(message, error.Print());
    }

    public static Task FailTask<T>(
        this ITaskMessaging messaging,
        IOperationTaskMessage message,
        string errorMessage,
        Validation<Error, T> validation) =>
        messaging.FailTask(message, Error.New(errorMessage, Error.Many(validation.FailToSeq())));
}
