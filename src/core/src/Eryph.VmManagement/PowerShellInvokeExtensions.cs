using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.VmManagement;

internal static class PowerShellInvokeExtensions
{
    public static Either<PowershellFailure, Seq<TypedPsObject<T>>> GetObjects<T>(
        this PowerShell ps, IEnumerable input,  ILogger log, IPsObjectRegistry registry)
    {
        return InvokeGetObjects(ps, ps.InvokeTyped<T>(input, registry), log);
    }

    public static async Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetObjectsAsync<T>(
        this PowerShell ps, IEnumerable input, ILogger log, IPsObjectRegistry registry)
    {
        using var inputData = new PSDataCollection<PSObject>();

        foreach (var i in input)
        {
            if(i is PSObject psObject)
                inputData.Add(psObject);
            else
                inputData.Add(new PSObject(i));
        }
        inputData.Complete();

        var tryResult = await
            Prelude.TryAsync(() =>
            {
                return inputData.Count == 0
                    ? ps.InvokeAsync()
                    : ps.InvokeAsync(inputData);
            }).Try().ConfigureAwait(false);

        var result = tryResult.Match<Either<PowershellFailure, Seq<TypedPsObject<T>>>>(
            Succ: s =>
            {
                
                var res = s.ToArray().Map(x => new TypedPsObject<T>(x, registry)).ToSeq();
                s.Dispose();
                return res;
            },
            Fail: ex => ExceptionToPowershellFailure(ex, log));

        return HandlePowershellErrors(ps, result, log);
    }

    public static Either<PowershellFailure, Unit> Run(this PowerShell ps, IEnumerable input, ILogger log)
    {
        return HandlePowershellErrors(ps,
            Prelude.Try(() => ps.Invoke(input)).Try().Match<Either<PowershellFailure, Unit>>(
                Succ: s => Unit.Default,
                Fail: ex => ExceptionToPowershellFailure(ex, log)), log);
    }

    public static async Task<Either<PowershellFailure, Unit>> RunAsync(this PowerShell ps,IEnumerable input, ILogger log)
    {
        using var inputData = new PSDataCollection<PSObject>();

        foreach (var i in input)
        {
            if (i is PSObject psObject)
                inputData.Add(psObject);
            else
                inputData.Add(new PSObject(i));
        }

        inputData.Complete();

        var tryResult = await
            Prelude.TryAsync(() => inputData.Count == 0 ? ps.InvokeAsync() : ps.InvokeAsync(inputData) 
            
            ).Try().ConfigureAwait(false);

        var result = tryResult.Match<Either<PowershellFailure, Unit>>(
            Succ: s =>
            {
                s.Iter(o => o.DisposeObject());
                s.Dispose();
                return Unit.Default;
            },
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

        if (result.IsLeft)
        {
            ps.Streams.ClearStreams();
            return result;
        }

        var error = ps.Streams.Error.FirstOrDefault();

        if (error != null)
        {
            var message =
                $" Command: {error.InvocationInfo?.MyCommand}, Error: {error}, Exception: {error.Exception}";

            log.LogError(error.Exception, message);

            ps.Streams.ClearStreams();

            return new PowershellFailure
            {
                Message = message
            };
        }

        ps.Streams.ClearStreams();
        return result;
    }

    public static Try<Seq<TypedPsObject<T>>> InvokeTyped<T>(this PowerShell ps, IEnumerable input, IPsObjectRegistry registry)
    {
        return Prelude.Try( () =>
        {
            var invoked =  ps.Invoke(input);
            var typed = invoked.Map(x => new TypedPsObject<T>(x, registry)).ToSeq();
            
            return typed;
        });
    }
}