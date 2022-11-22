using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.VmManagement
{
    public class PowershellEngine : IPowershellEngine, IDisposable
    {
        private RunspacePool _runspace;
        private SemaphoreSlim _semaphore = new(1);
        private ILogger _log;

        public PowershellEngine(ILogger log)
        {
            _log = log;

        }


        public void Dispose()
        {
            if ((_runspace?.IsDisposed).GetValueOrDefault(true))
                return;

            try
            {
                _runspace?.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        public Either<PowershellFailure, Seq<TypedPsObject<T>>> GetObjects<T>(PsCommandBuilder builder,
            Action<int> reportProgress = null)
        {
            using var ps = CreateShell().GetAwaiter().GetResult();
            builder.Build(ps);
            InitializeProgressReporting(ps, reportProgress);
            return ps.GetObjects<T>(_log);
        }

        public async Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetObjectsAsync<T>(PsCommandBuilder builder,
            Func<int, Task> reportProgress = null)
        {
            using var ps = await CreateShell();

            builder.Build(ps);
            InitializeAsyncProgressReporting(ps, reportProgress);

            return await ps.GetObjectsAsync<T>(_log).ConfigureAwait(false);
        }

        public Either<PowershellFailure, Unit> Run(PsCommandBuilder builder, Action<int> reportProgress = null)
        {
            using var ps = CreateShell().GetAwaiter().GetResult();
            builder.Build(ps);
                
            InitializeProgressReporting(ps, reportProgress);
            return ps.Run(_log);
        }

        public async Task<Either<PowershellFailure, Unit>> RunAsync(PsCommandBuilder builder,
            Func<int, Task> reportProgress = null)
        {
            using var ps = await CreateShell();
            builder.Build(ps);

            InitializeAsyncProgressReporting(ps, reportProgress);
            return await ps.RunAsync(_log).ConfigureAwait(false);
        }

        public async Task<PowerShell> CreateShell()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_runspace == null)
                {
                    
                    var iss = InitialSessionState.CreateDefault();
                    iss.ExecutionPolicy = ExecutionPolicy.RemoteSigned;
                    iss.ApartmentState = ApartmentState.MTA;
                    
                    _runspace = RunspaceFactory.CreateRunspacePool(iss);
                    await Task.Factory.FromAsync(_runspace.BeginOpen, _runspace.EndOpen, null);

                    using var tempShell = PowerShell.Create();
                    tempShell.RunspacePool = _runspace;
                    tempShell.AddScript("import-module Hyper-V -RequiredVersion 2.0.0.0");
                    tempShell.Invoke();
                    tempShell.AddScript("disable-vmeventing -Force");
                    tempShell.Invoke();
                
                }

                var ps = PowerShell.Create();
                ps.RunspacePool = _runspace;

                return ps;

            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static void InitializeProgressReporting(PowerShell ps, Action<int> reportProgress)
        {
            if (reportProgress == null)
                return;

            ps.Streams.Progress.DataAdded += (sender, eventargs) =>
            {
                var progressRecords = (PSDataCollection<ProgressRecord>) sender;
                reportProgress(progressRecords[eventargs.Index].PercentComplete);
            };
        }

        private static void InitializeAsyncProgressReporting(PowerShell ps, Func<int, Task> reportProgress)
        {
            if (reportProgress == null)
                return;


            ps.Streams.Progress.DataAdded += async (sender, eventargs) =>
            {
                var progressRecords = (PSDataCollection<ProgressRecord>) sender;
                var percent = progressRecords[eventargs.Index].PercentComplete;
                if (percent == 0 || percent == 100)
                    return;

                await reportProgress(percent).ConfigureAwait(false);
            };
        }
    }
}