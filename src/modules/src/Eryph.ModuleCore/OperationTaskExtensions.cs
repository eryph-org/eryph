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
}