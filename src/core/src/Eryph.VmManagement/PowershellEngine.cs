using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell;
using Microsoft.PowerShell.Commands;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Eryph.VmManagement
{
    public sealed class PowershellEngine : IPowershellEngine, IDisposable, IPsObjectRegistry
    {
        private RunspacePool _runspace;
        private SemaphoreSlim _semaphore = new(1);
        private ILogger _log;
        private Seq<WeakReference<PSObject>> _createdObjects;

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
                _runspace?.Close();
                _runspace?.Dispose();

                foreach (var reference in _createdObjects)
                {
                    if (!reference.TryGetTarget(out var psObject)) continue;

                    psObject.DisposeObject();
                }

                _createdObjects = default;
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
            var input = builder.Build(ps);
            InitializeProgressReporting(ps, reportProgress);
            return ps.GetObjects<T>(input, _log, this);
        }

        public async Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetObjectsAsync<T>(
            PsCommandBuilder builder,
            Func<int, Task> reportProgress = null)
        {
            using var ps = await CreateShell();


            var input = builder.Build(ps);
            InitializeAsyncProgressReporting(ps, reportProgress);

            var res = await ps.GetObjectsAsync<T>(input, _log, this).ConfigureAwait(false);
            
            return res;
        }

        public Either<PowershellFailure, Unit> Run(PsCommandBuilder builder, Action<int> reportProgress = null)
        {
            using var ps = CreateShell().GetAwaiter().GetResult();
            var input = builder.Build(ps);
                
            InitializeProgressReporting(ps, reportProgress);
            return ps.Run(input, _log);
        }

        public async Task<Either<PowershellFailure, Unit>> RunAsync(PsCommandBuilder builder,
            Func<int, Task> reportProgress = null)
        {
            using var ps = await CreateShell();
            var input = builder.Build(ps);

            InitializeAsyncProgressReporting(ps, reportProgress);
            return await ps.RunAsync(input, _log).ConfigureAwait(false);
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
                    iss.ImportPSModule(new List<ModuleSpecification>(new []
                    {
                        new ModuleSpecification(new Hashtable(
                            new Dictionary<string, string>
                            {
                                {"ModuleName", "Hyper-V"},
                                {"ModuleVersion", "2.0.0.0"}

                            }))
                    }));


                    _runspace = RunspaceFactory.CreateRunspacePool(iss);
                    await Task.Factory.FromAsync(_runspace.BeginOpen, _runspace.EndOpen, null);

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

        public void AddPsObject(PSObject psObject)
        {
            _createdObjects = _createdObjects.Add(new WeakReference<PSObject>(psObject));
        }


    }


}