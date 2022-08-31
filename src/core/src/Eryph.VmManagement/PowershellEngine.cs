using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.Extensions.Logging;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.VmManagement
{
    public class PowershellEngine : IPowershellEngine, IDisposable
    {
        private readonly RunspacePool _runspace;
        private ILogger _log;

        public PowershellEngine(ILogger log)
        {
            _log = log;
            var iss = InitialSessionState.CreateDefault2();
            _runspace = RunspaceFactory.CreateRunspacePool(iss);
            _runspace.Open();

            using (var ps = CreateShell())
            {
                ps.AddScript("import-module Hyper-V -RequiredVersion 2.0.0.0");
                ps.Invoke();
                ps.AddScript("disable-vmeventing -Force");
                ps.Invoke();
            }
        }

        public void Dispose()
        {
            _runspace?.Dispose();
        }

        public Either<PowershellFailure, Seq<TypedPsObject<T>>> GetObjects<T>(PsCommandBuilder builder,
            Action<int> reportProgress = null)
        {
            using var ps = CreateShell();
            builder.Build(ps);
            InitializeProgressReporting(ps, reportProgress);
            return ps.GetObjects<T>(_log);
        }

        public async Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetObjectsAsync<T>(PsCommandBuilder builder,
            Func<int, Task> reportProgress = null)
        {
            using var ps = CreateShell();

            builder.Build(ps);
            InitializeAsyncProgressReporting(ps, reportProgress);

            return await ps.GetObjectsAsync<T>(_log).ConfigureAwait(false);
        }

        public Either<PowershellFailure, Unit> Run(PsCommandBuilder builder, Action<int> reportProgress = null)
        {
            using var ps = CreateShell();
            builder.Build(ps);
                
            InitializeProgressReporting(ps, reportProgress);
            return ps.Run(_log);
        }

        public async Task<Either<PowershellFailure, Unit>> RunAsync(PsCommandBuilder builder,
            Func<int, Task> reportProgress = null)
        {
            using var ps = CreateShell();
            builder.Build(ps);

            InitializeAsyncProgressReporting(ps, reportProgress);
            return await ps.RunAsync(_log).ConfigureAwait(false);
        }

        public PowerShell CreateShell()
        {
            var ps = PowerShell.Create();
            ps.RunspacePool = _runspace;

            return ps;
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