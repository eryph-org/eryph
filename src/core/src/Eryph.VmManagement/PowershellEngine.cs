using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell;
using Microsoft.PowerShell.Commands;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public sealed class PowershellEngine(
    ILogger logger)
    : IPowershellEngine, IDisposable, IPsObjectRegistry
{
    private RunspacePool _runspacePool;
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly IList<PSObject> _createdObjects = new List<PSObject>();

    public ITypedPsObjectMapping ObjectMapping { get; } = new TypedPsObjectMapping(logger);

    public EitherAsync<PowershellFailure, Option<TypedPsObject<T>>> GetObjectAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default) =>
        from results in GetObjectsAsync<T>(builder, reportProgress, cancellationToken)
        from _ in guard(results.Count <= 1,
            new PowershellFailure($"Powershell returned multiple values when fetching {typeof(T).Name}."))
        select results.HeadOrNone();

    public EitherAsync<PowershellFailure, Seq<TypedPsObject<T>>> GetObjectsAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default) =>
        from results in TryAsync(async () =>
            {
                var output = await ExecuteAsync(builder, reportProgress, cancellationToken);
                return output.Map(x => new TypedPsObject<T>(x, this, ObjectMapping)).ToSeq().Strict();
            })
            .ToEither()
            .MapLeft(e => ToFailure(e))
            .BindLeft(f => f.Category switch
            {
                PowershellFailureCategory.ObjectNotFound =>
                    RightAsync<PowershellFailure, Seq<TypedPsObject<T>>>(Empty),
                _ => LeftAsync<PowershellFailure, Seq<TypedPsObject<T>>>(f),
            })
        select results;

    public EitherAsync<PowershellFailure, Option<T>> GetObjectValueAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default) =>
        GetObjectAsync<T>(builder, reportProgress, cancellationToken)
            .Map(result => result.Map(x => x.Value));

    public EitherAsync<PowershellFailure, Seq<T>> GetObjectValuesAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default) =>
        GetObjectsAsync<T>(builder, reportProgress, cancellationToken)
            .Map(result => result.Map(seq => seq.Map(x => x.Value)).Strict());

    public EitherAsync<PowershellFailure, Unit> RunAsync(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        CancellationToken cancellationToken = default) =>
        TryAsync(async () =>
        {
            var outputs = await ExecuteAsync(builder, reportProgress, cancellationToken);
            foreach (var output in outputs)
            {
                output.DisposeObject();
            }

            return unit;
        }).ToEither().MapLeft(e => ToFailure(e));

    public void AddPsObject(PSObject psObject)
    {
        _createdObjects.Add(psObject);
    }

    private async Task<IList<PSObject>> ExecuteAsync(
        PsCommandBuilder builder,
        [CanBeNull] Func<int, Task> reportProgress,
        CancellationToken cancellationToken)
    {
        using var powerShell = await CreatePowerShell();
        using var _ = InitializeProgressReporting(powerShell, reportProgress);
        
        var inputs = builder.Build(powerShell);
        using var inputData = new PSDataCollection<PSObject>(
            inputs.Map(input => input is PSObject pso ? pso : new PSObject(input)));

        var invocationSettings = new PSInvocationSettings()
        {
            ErrorActionPreference = ActionPreference.Stop,
        };

        IList<PSObject> outputs;
        IList<ErrorRecord> errors;
        try
        {
            var task = powerShell.InvokeAsync(inputData, invocationSettings, null, null);

            await ((Task)task.WaitAsync(cancellationToken))
                .ConfigureAwait(options: ConfigureAwaitOptions.SuppressThrowing);
            if (cancellationToken.IsCancellationRequested)
            {
                await powerShell.StopAsync(null, null);
            }

            // The 'await task' will throw a PipelineStoppedException when the pipeline
            // has been stopped with StopAsync.
            using var outputData = await task;
            
            outputs = outputData.ToList();
            errors = powerShell.Streams.Error.ToList();
        }
        catch (RuntimeException ex) when (ex is not PipelineStoppedException)
        {
            logger.LogInformation(ex, "Powershell command '{Command}' failed: {Error}.",
                ex.ErrorRecord.InvocationInfo?.MyCommand, ex.ErrorRecord.ToString());
            throw;
        }
        catch (Exception ex) when (ex is not PipelineStoppedException)
        {
            logger.LogInformation(ex, "Powershell failed with exception.");
            throw;
        }

        foreach (var error in errors)
        {
            logger.LogInformation(error.Exception, "Powershell command '{Command}' failed: {Error}.",
                error.InvocationInfo?.MyCommand, error.ToString());
        }

        var bestError = errors.FirstOrDefault(e => e.CategoryInfo.Category != ErrorCategory.ObjectNotFound)
            ?? errors.FirstOrDefault(e => string.IsNullOrEmpty(e.CategoryInfo.Activity))
            ?? errors.FirstOrDefault();

        if (bestError is null)
            return outputs;

        foreach (var output in outputs)
        {
            output.DisposeObject();
        }
        throw new PsErrorException(bestError);
    }

    private async Task<PowerShell> CreatePowerShell()
    {
        var runspacePool = await GetRunspacePool();
        var ps = PowerShell.Create();
        ps.RunspacePool = runspacePool;
        return ps;
    }
    
    private async Task<RunspacePool> GetRunspacePool()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_runspacePool is not null)
                return _runspacePool;

            var iss = InitialSessionState.CreateDefault();
            iss.ExecutionPolicy = ExecutionPolicy.RemoteSigned;

            if (IsWindows2016)
            {
                iss.Variables.Add(new SessionStateVariableEntry(
                    "PSDefaultParameterValues",
                    new Hashtable { ["Import-Module:SkipEditionCheck"] = true },
                    ""));
            }

            iss.ImportPSModule(
            [
                new ModuleSpecification(new Hashtable
                {
                    ["ModuleName"] = "Hyper-V" ,
                    ["ModuleVersion"] = "2.0.0.0",
                })
            ]);

            _runspacePool = RunspaceFactory.CreateRunspacePool(iss);
            await Task.Factory.FromAsync(_runspacePool.BeginOpen, _runspacePool.EndOpen, null);

            return _runspacePool;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static bool IsWindows2016 =>
        // The build number of Windows Server 2016 (and the corresponding Windows 10 release) is 14393
        Environment.OSVersion.Version.Build <= 14393;

    private IDisposable InitializeProgressReporting(
        PowerShell ps,
        [CanBeNull] Func<int, Task> reportProgress) =>
        reportProgress switch
        {
            not null => Observable
                .FromEventPattern<EventHandler<DataAddingEventArgs>, DataAddingEventArgs>(
                    h => ps.Streams.Progress.DataAdding += h, h => ps.Streams.Progress.DataAdding -= h)
                .SelectMany(e => Observable.FromAsync(async () =>
                {
                    var progress = (ProgressRecord)e.EventArgs.ItemAdded;
                    if (progress is not { ParentActivityId: < 0, PercentComplete: > 0 and < 100 })
                        return;

                    await reportProgress!(progress.PercentComplete).ConfigureAwait(false);
                }))
                .Subscribe(
                    onNext: _ => {},
                    onError: ex => logger.LogError(ex, "Failed to process Powershell progress record.")),
            _ => Disposable.Empty,
        };

    /// <summary>
    /// Converts a <see cref="Exception"/> to a <see cref="PowershellFailure"/>.
    /// </summary>
    /// <remarks>
    /// We need to be careful when we return <see cref="PowershellFailureCategory.ObjectNotFound"/>.
    /// <see cref="ErrorCategory.ObjectNotFound"/> is used fo a lot of different errors including
    /// when a command does not exist. We additionally check <see cref="ErrorCategoryInfo.Activity"/>.
    /// When it is present, the error has been raised by a properly executed command.
    /// </remarks>
    private static PowershellFailure ToFailure(
        Exception exception) =>
        exception switch
        {
            PsErrorException pee => ToFailure(pee.Error),
            PipelineStoppedException _ => new PowershellFailure(
                "The Powershell pipeline has been cancelled.",
                PowershellFailureCategory.PipelineStopped),
            RuntimeException rex => ToFailure(rex.ErrorRecord),
            _ => new PowershellFailure(
                exception.Message,
                PowershellFailureCategory.Other)
        };

    private static PowershellFailure ToFailure(
        ErrorRecord errorRecord) =>
        new PowershellFailure(
            $"Powershell command '{errorRecord.InvocationInfo?.MyCommand}' failed: {errorRecord}.",
            ToFailureCategory(errorRecord.CategoryInfo));

    private static PowershellFailureCategory ToFailureCategory(
        ErrorCategoryInfo categoryInfo) =>
        notEmpty(categoryInfo.Activity) && categoryInfo.Category is ErrorCategory.ObjectNotFound
            ? PowershellFailureCategory.ObjectNotFound
            : PowershellFailureCategory.Other;

    public void Dispose()
    {
        _semaphore.Dispose();
        _runspacePool.Dispose();
        foreach (var psObject in _createdObjects)
        {
            psObject.DisposeObject();
        }
    }

    /// <summary>
    /// This exception shortly holds an <see cref="ErrorRecord"/> and allows
    /// us to pass it to the caller.
    /// </summary>
    /// <remarks>
    /// This exception should never leave the <see cref="PowershellEngine"/>.
    /// </remarks>
#pragma warning disable S3871 // Exception types should be "public"
    private sealed class PsErrorException(
#pragma warning restore S3871 // Exception types should be "public"
        ErrorRecord errorRecord) :
        Exception("PowerShell returned one or more non-terminating errors.")
    {
        public ErrorRecord Error { get; } = errorRecord;
    }
}
