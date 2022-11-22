using System;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.VmManagement;

internal static class PowerShellInvokeExtensions
{
    public static Either<PowershellFailure, Seq<TypedPsObject<T>>> GetObjects<T>(this PowerShell ps, ILogger log)
    {
        return InvokeGetObjects(ps, ps.InvokeTyped<T>(), log);
    }

    public static async Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetObjectsAsync<T>(
        this PowerShell ps, ILogger log)
    {
        var tryResult = await
            Prelude.TryAsync(Task.Factory.FromAsync(ps.BeginInvoke(), ps.EndInvoke)).Try().ConfigureAwait(false);

        var result = tryResult.Match<Either<PowershellFailure, Seq<TypedPsObject<T>>>>(
            Succ: s => s.Map(x => new TypedPsObject<T>(x)).ToSeq(),
            Fail: ex => ExceptionToPowershellFailure(ex, log));

        return HandlePowershellErrors(ps, result, log);
    }

    public static Either<PowershellFailure, Unit> Run(this PowerShell ps, ILogger log)
    {
        return HandlePowershellErrors(ps,
            Prelude.Try(ps.Invoke).Try().Match<Either<PowershellFailure, Unit>>(
                Succ: s => Unit.Default,
                Fail: ex => ExceptionToPowershellFailure(ex, log)), log);
    }

    public static async Task<Either<PowershellFailure, Unit>> RunAsync(this PowerShell ps, ILogger log)
    {

        var tryResult = await
            Prelude.TryAsync(Task.Factory.FromAsync(ps.BeginInvoke(), ps.EndInvoke)).Try().ConfigureAwait(false);

        var result = tryResult.Match<Either<PowershellFailure, Unit>>(
            Succ: s => Unit.Default,
            Fail: ex => ExceptionToPowershellFailure(ex, log));

        return HandlePowershellErrors(ps, result, log);
    }

    private static Either<PowershellFailure, Seq<TypedPsObject<T>>> InvokeGetObjects<T>(this PowerShell ps,
        Try<Seq<TypedPsObject<T>>> invokeFunc, ILogger log)
    {
        return HandlePowershellErrors(ps, invokeFunc.ToEither(ex =>ExceptionToPowershellFailure(ex, log)), log);
    }


    private static PowershellFailure ExceptionToPowershellFailure(Exception ex, ILogger log)
    {
        log?.LogError(ex, ex.Message);
        return new PowershellFailure {Message = ex.Message};
    }

    private static Either<PowershellFailure, TResult> HandlePowershellErrors<TResult>(PowerShell ps,
        Either<PowershellFailure, TResult> result, ILogger log)
    {
        return result;

        if (result.IsRight) return result;
        
        var error = ps.Streams.Error.FirstOrDefault() 
                    ?? new ErrorRecord(
                        new Exception("unknown powershell error"), "", ErrorCategory.NotSpecified, null);

        var message =
            $" Command: {error.InvocationInfo?.MyCommand}, Error: {error}, Exception: {error.Exception}";

        log.LogError(error.Exception,message);

        return new PowershellFailure
        {
            Message = message
        };

    }

    public static Try<Seq<TypedPsObject<T>>> InvokeTyped<T>(this PowerShell ps)
    {
        return Prelude.Try(ps.Invoke().Map(x => new TypedPsObject<T>(x)).ToSeq());
    }
}