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
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell;
using Microsoft.PowerShell.Commands;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public sealed class PowershellEngine(
    ILogger logger,
    IPowershellEngineLock engineLock)
    : IPowershellEngine, IDisposable, IPsObjectRegistry
{
    private RunspacePool _runspacePool;
    private readonly SemaphoreSlim _runspaceSemaphore = new(1, 1);
    private readonly IList<PSObject> _createdObjects = new List<PSObject>();

    public ITypedPsObjectMapping ObjectMapping { get; } = new TypedPsObjectMapping(logger);

    public EitherAsync<Error, Seq<TypedPsObject<T>>> GetObjectsAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        bool withoutLock = false,
        CancellationToken cancellationToken = default) =>
        from results in TryAsync(async () =>
            {
                var output = await ExecuteAsync(builder, reportProgress, withoutLock, cancellationToken);
                return output.Map(x => new TypedPsObject<T>(x, this, ObjectMapping)).ToSeq().Strict();
            })
            .ToEither()
            .MapLeft(ToPowershellError)
            // We need to be careful with ObjectNotFound. Powershell uses the corresponding
            // category for other reasons as well (e.g. when a command is not found).
            // We additionally check that the Activity is set to ensure that the error has
            // been raised by a properly executed command.
            .BindLeft(e => e is PowershellError { Activity.IsSome: true } pse
                           && (pse.Category == PowershellErrorCategory.ObjectNotFound
                               // Hyper-V Cmdlets sometimes use the category
                               // InvalidArgument when the entity does not exist.
                               || pse.Reason == "VirtualizationException" && pse.Category == PowershellErrorCategory.InvalidArgument)
                ? RightAsync<Error, Seq<TypedPsObject<T>>>(Empty)
                : LeftAsync<Error, Seq<TypedPsObject<T>>>(e))
        select results;

    public EitherAsync<Error, Seq<T>> GetObjectValuesAsync<T>(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        bool withoutLock = false,
        CancellationToken cancellationToken = default) =>
        from objects in GetObjectsAsync<T>(builder, reportProgress, withoutLock, cancellationToken)
        let result = objects.Map(x => x.Value).Strict()
        select result;

    public EitherAsync<Error, Unit> RunAsync(
        PsCommandBuilder builder,
        Func<int, Task> reportProgress = null,
        bool withoutLock = false,
        CancellationToken cancellationToken = default) =>
        TryAsync(async () =>
        {
            var outputs = await ExecuteAsync(builder, reportProgress, withoutLock, cancellationToken);
            foreach (var output in outputs)
            {
                output.DisposeObject();
            }

            return unit;
        }).ToEither().MapLeft(ToPowershellError);

    public void AddPsObject(PSObject psObject)
    {
        _createdObjects.Add(psObject);
    }

    private async Task<IList<PSObject>> ExecuteAsync(
        PsCommandBuilder builder,
        [CanBeNull] Func<int, Task> reportProgress,
        bool withoutLock,
        CancellationToken cancellationToken)
    {
        if (!withoutLock)
            await engineLock.AcquireLockAsync(cancellationToken);

        try
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

            List<PSObject> outputs;
            List<ErrorRecord> errors;
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
            catch (RuntimeException rex) when (rex is not PipelineStoppedException)
            {
                logger.LogDebug(rex, "Powershell command '{Command}' failed: {Error}",
                    rex.ErrorRecord.InvocationInfo?.MyCommand, rex.ErrorRecord.ToString());
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException or PipelineStoppedException)
            {
                logger.LogDebug(ex, "Powershell failed with exception.");
                throw;
            }

            foreach (var error in errors)
            {
                logger.LogDebug(error.Exception, "Powershell command '{Command}' failed: {Error}",
                    error.InvocationInfo?.MyCommand, error.ToString());
            }

            if (errors.Count == 0)
                return outputs;

            foreach (var output in outputs)
            {
                output.DisposeObject();
            }

            throw new PsErrorException(errors);
        }
        finally
        {
            if (!withoutLock)
                engineLock.ReleaseLock();
        }
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
        await _runspaceSemaphore.WaitAsync();
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
            
            iss.ThrowOnRunspaceOpenError = true;

            _runspacePool = RunspaceFactory.CreateRunspacePool(iss);
            await Task.Factory.FromAsync(_runspacePool.BeginOpen, _runspacePool.EndOpen, null);

            return _runspacePool;
        }
        finally
        {
            _runspaceSemaphore.Release();
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
    /// Converts an <see cref="Error"/> to a <see cref="PowershellError"/>.
    /// </summary>
    private static Error ToPowershellError(Error error) =>
        error switch
        {
            { Exception.Case: PsErrorException pee } =>
                Error.Many(pee.ErrorRecords.ToSeq().Map<Error>(PowershellError.New)),
            { Exception.Case: RuntimeException rex } => PowershellError.New(rex),
            { Exception.Case: OperationCanceledException oce } => new PowershellError(
                "The operation has been cancelled before the global lock could be acquired.",
                oce.HResult,
                None,
                PowershellErrorCategory.PipelineStopped,
                None,
                None,
                None),
            _ => Error.New("Unexpected exception in Powershell engine.", error),
        };

    public void Dispose()
    {
        _runspaceSemaphore.Dispose();
        _runspacePool?.Dispose();
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
        IReadOnlyList<ErrorRecord> errorRecords) :
        Exception("PowerShell returned one or more non-terminating errors.")
    {
        public IReadOnlyList<ErrorRecord> ErrorRecords { get; } = errorRecords;
    }
}
