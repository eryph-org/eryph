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
    public static async Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetObjectsAsync<T>(
        this PowerShell ps,
        IEnumerable input,
        ILogger log,
        IPsObjectRegistry registry,
        ITypedPsObjectMapping mapping)
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

        var result = tryResult.Match(
            Succ: s =>
            {
                return Prelude.Try( () =>
                {
                    var r = s.ToArray().Map(x => new TypedPsObject<T>(x, registry, mapping)).ToSeq();
                    s.Dispose();
                    return r;
                }).ToEither(ToFailure);
            },
            Fail: ex => ToFailure(ex));

        return HandlePowershellErrors(ps, result, log);
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
            Fail: ex => ToFailure(ex));

        return HandlePowershellErrors(ps, result, log);
    }

    // TODO improve me
    private static PowershellFailure ToFailure(
        this Exception exception) =>
        new PowershellFailure(
            exception.Message,
            PowershellFailureCategory.NotSpecified);


    // TODO Do we need the logging here? This might be confusing as the error by the Powershell command might be expected
    /*
    private static PowershellFailure ExceptionToPowershellFailure(Exception ex, ILogger log)
    {
        log?.LogError(ex, ex.Message);
        return new PowershellFailure {Message = ex.Message};
    }
    */

    private static Either<PowershellFailure, TResult> HandlePowershellErrors<TResult>(
        PowerShell ps,
        Either<PowershellFailure, TResult> result,
        ILogger log)
    {
        var handledResult = result
            .Bind(r => HandleErrors<TResult>(r, ps.Streams.Error.ToSeq()));

        ps.Streams.ClearStreams();
        
        return handledResult;
    }

    private static Either<PowershellFailure, TResult> HandleErrors<TResult>(
        Either<PowershellFailure, TResult> result,
        Seq<ErrorRecord> errors) =>
        from value in result
        from _ in errors
            .Map(e => new PowershellFailure(
                $"Command: {e.InvocationInfo?.MyCommand}, Error: {e}, Exception: {e.Exception}",
                e.CategoryInfo.Category.ToFailureCategory()))
            .OrderByDescending(f => f.Category != PowershellFailureCategory.ObjectNotFound)
            .Map(Prelude.Left<PowershellFailure, TResult>)
            .Sequence()
        select value;
}
