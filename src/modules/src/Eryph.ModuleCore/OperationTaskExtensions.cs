using System;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

// ReSharper disable once CheckNamespace
namespace Dbosoft.Rebus.Operations;

#pragma warning restore 1998
public static class OperationTaskExtensions
{
  
    public static Task<Unit> FailOrComplete<TRet>(
       this EitherAsync<Error, TRet> either, ITaskMessaging messaging, IOperationTaskMessage message)
        where TRet: notnull
    {
        return either.MatchAsync(
            LeftAsync: l => messaging.FailTask(message,l),
            RightAsync: ret => ret is Unit 
                ? messaging.CompleteTask(message) 
                : messaging.CompleteTask(message, ret));

    }

    public static Task FailTask(this ITaskMessaging messaging, IOperationTaskMessage message, Error error)
    {
        return messaging.FailTask(message, error.Message);
    }

    public static Task FailTask<T>(
        this ITaskMessaging messaging,
        IOperationTaskMessage message,
        Validation<Error, T> validation) => 
        validation.Match(
            Succ: _ => throw new ArgumentException("The validation must have failed.", nameof(validation)),
            Fail: errors => messaging.FailTask(message, $"One or more validations have failed: {errors.ToFullArrayString()}"));
}