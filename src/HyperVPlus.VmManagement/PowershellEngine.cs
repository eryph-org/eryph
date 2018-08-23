using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using LanguageExt;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace HyperVPlus.VmManagement
{
    public interface IPowershellEngine
    {
        Either<PowershellFailure, IEnumerable<TypedPsObject<T>>> GetObjects<T>(PsCommandBuilder builder, Action<int> reportProgress = null);
        Either<PowershellFailure, Unit> Run(PsCommandBuilder builder,  Action<int> reportProgress = null);
        Task<Either<PowershellFailure, IEnumerable<TypedPsObject<T>>>> GetObjectsAsync<T>(PsCommandBuilder builder, Func<int, Task> reportProgress = null);
        Task<Either<PowershellFailure, Unit>> RunAsync(PsCommandBuilder builder, Func<int, Task> reportProgress = null);

        Either<PowershellFailure, TypedPsObject<T>> GetObject<T>(PsCommandBuilder builder, Action<int> reportProgress = null);
        Task<Either<PowershellFailure, TypedPsObject<T>>> GetObjectAsync<T>(PsCommandBuilder builder, Func<int, Task> reportProgress = null);

    }

    public class PowershellEngine :  IPowershellEngine
    {
        private readonly RunspacePool _runspace;

        public PowershellEngine()
        {
            var iss = InitialSessionState.CreateDefault2();
            _runspace = RunspaceFactory.CreateRunspacePool(iss);
            _runspace.Open();

            using (var ps = CreateShell())
            {
                ps.AddScript("import-module Hyper-V -RequiredVersion 1.1");
                ps.Invoke();
            }

        }

        public PowerShell CreateShell()
        {

            var ps = PowerShell.Create();
            ps.RunspacePool = _runspace;

            return ps;
        }

        public Either<PowershellFailure, IEnumerable<TypedPsObject<T>>> GetObjects<T>(PsCommandBuilder builder, Action<int> reportProgress= null)
        {
            using (var ps = CreateShell())
            {
                builder.Build(ps);

                InitializeProgressReporting(ps, reportProgress);
                return ps.GetObjects<T>();
            }
        }

        public async Task<Either<PowershellFailure, IEnumerable<TypedPsObject<T>>>> GetObjectsAsync<T>(PsCommandBuilder builder, Func<int, Task> reportProgress = null)
        {
            using (var ps = CreateShell())
            {
                builder.Build(ps);
                InitializeAsyncProgressReporting(ps, reportProgress);

                return await ps.GetObjectsAsync<T>().ConfigureAwait(false);
            }
        }

        public Either<PowershellFailure, TypedPsObject<T>> GetObject<T>(PsCommandBuilder builder, Action<int> reportProgress = null)
        {
            using (var ps = CreateShell())
            {
                builder.Build(ps);

                InitializeProgressReporting(ps, reportProgress);
                return ps.GetObject<T>();
            }
        }

        public async Task<Either<PowershellFailure, TypedPsObject<T>>> GetObjectAsync<T>(PsCommandBuilder builder, Func<int, Task> reportProgress = null)
        {
            using (var ps = CreateShell())
            {
                builder.Build(ps);
                InitializeAsyncProgressReporting(ps, reportProgress);

                return await ps.GetObjectAsync<T>().ConfigureAwait(false);
            }
        }

        public Either<PowershellFailure, Unit> Run(PsCommandBuilder builder, Action<int> reportProgress = null)
        {
            using (var ps = CreateShell())
            {
                builder.Build(ps);

                InitializeProgressReporting(ps, reportProgress);
                return ps.Run();
            }
        }

        public async Task<Either<PowershellFailure, Unit>> RunAsync(PsCommandBuilder builder, Func<int, Task> reportProgress = null)
        {
            using (var ps = CreateShell())
            {
                builder.Build(ps);

                InitializeAsyncProgressReporting(ps, reportProgress);
                return await ps.RunAsync().ConfigureAwait(false);
            }
        }

        private static void InitializeProgressReporting(PowerShell ps, Action<int> reportProgress)
        {
            if (reportProgress == null)
                return;

            ps.Streams.Progress.DataAdded += (sender, eventargs) => {
                var progressRecords = (PSDataCollection<ProgressRecord>)sender;
                reportProgress(progressRecords[eventargs.Index].PercentComplete);
            };
        }

        private static void InitializeAsyncProgressReporting(PowerShell ps, Func<int, Task> reportProgress)
        {
            if (reportProgress == null)
                return;


            ps.Streams.Progress.DataAdded += async (sender, eventargs) => {
                var progressRecords = (PSDataCollection<ProgressRecord>)sender;
                var percent = progressRecords[eventargs.Index].PercentComplete;
                if (percent == 0 || percent == 100)
                    return;

                await reportProgress(percent).ConfigureAwait(false);
            };

        }

    }

    public class PsCommandBuilder
    {
        public static PsCommandBuilder Create()
        {
            return new PsCommandBuilder();
        }

        private readonly List<Tuple<DataType,object,string>> _dataChain = new List<Tuple<DataType, object, string>>();

        public PsCommandBuilder()
        {
        }

        public PsCommandBuilder AddCommand(string command)
        {
            _dataChain.Add(new Tuple<DataType, object, string>(DataType.Command,null,command));
            return this;
        }

        public PsCommandBuilder AddParameter(string parameter, object value)
        {
            _dataChain.Add(new Tuple<DataType, object, string>(DataType.Parameter, value, parameter));
            return this;
        }

        public PsCommandBuilder AddArgument(object statement)
        {
            _dataChain.Add(new Tuple<DataType, object, string>(DataType.AddArgument, statement, null));
            return this;
        }

        public PsCommandBuilder Script(string script)
        {
            _dataChain.Add(new Tuple<DataType, object, string>(DataType.Script,null, script));
            return this;
        }

        private enum DataType
        {
            Command, 
            Parameter,
            AddArgument,
            Script,
        }

        public void Build(PowerShell ps)
        {
            foreach (var data in _dataChain)
            {
                switch (data.Item1)
                {
                    case DataType.Command:
                        ps.AddCommand(data.Item3);
                        break;
                    case DataType.Parameter:
                        ps.AddParameter(data.Item3, data.Item2);
                        break;
                    case DataType.AddArgument:
                        ps.AddArgument(data.Item2);
                        break;
                    case DataType.Script:
                        ps.AddScript(data.Item3);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }


    static class PowerShellInvokeExtensions
    {

        public static async Task<Either<PowershellFailure, TypedPsObject<T>>> GetObjectAsync<T>(this PowerShell ps) => 
            InvokeGetObject(ps, await ps.InvokeTypedAsync<T>().ConfigureAwait(false));
        
        public static Either<PowershellFailure, TypedPsObject<T>> GetObject<T>(this PowerShell ps) =>
            InvokeGetObject(ps, ps.InvokeTyped<T>());

        public static Either<PowershellFailure, IEnumerable<TypedPsObject<T>>> GetObjects<T>(this PowerShell ps) =>
            InvokeGetObjects(ps, ps.InvokeTyped<T>());

        public static async Task<Either<PowershellFailure, IEnumerable<TypedPsObject<T>>>> GetObjectsAsync<T>(this PowerShell ps) =>
            InvokeGetObjects(ps, await ps.InvokeTypedAsync<T>().ConfigureAwait(false));

        public static Either<PowershellFailure, Unit> Run(this PowerShell ps) =>
            Invoke(ps, Prelude.Try(ps.Invoke().AsEnumerable()));

        public static async Task<Either<PowershellFailure, Unit>> RunAsync(this PowerShell ps) =>
            Invoke(ps, Prelude.Try(
                (await Task.Factory.FromAsync(ps.BeginInvoke(), ps.EndInvoke).ConfigureAwait(false)).AsEnumerable()));

        private static Either<PowershellFailure, Unit> Invoke(this PowerShell ps, Try<IEnumerable<PSObject>> invokeFunc) =>
            HandlePowershellErrors(ps,
                invokeFunc.Try().Match<Either<PowershellFailure, Unit>>(
                    Succ: s => Unit.Default,
                    Fail: ex => ExceptionToPowershellFailure(ex)));


        private static Either<PowershellFailure, TypedPsObject<T>> InvokeGetObject<T>(this PowerShell ps, TryOption<IEnumerable<TypedPsObject<T>>> invokeFunc) =>
            HandlePowershellErrors(ps,
                invokeFunc.Try().Match(
                    None: () => new PowershellFailure { Message = "Empty result" },
                    Some: r =>
                    {
                        var resultArray = r.ToArray();
                        return Prelude.Try(resultArray.SingleOrDefault()).Try().Match<Either<PowershellFailure, TypedPsObject<T>>>(
                            Succ: sr => sr,
                            Fail: ex => new PowershellFailure { Message = $"Expected one result, but received {resultArray.Count()}" });
                    },
                    Fail: ex => ExceptionToPowershellFailure(ex)));


        private static Either<PowershellFailure, IEnumerable<TypedPsObject<T>>> InvokeGetObjects<T>(this PowerShell ps, TryOption<IEnumerable<TypedPsObject<T>>> invokeFunc) =>
            HandlePowershellErrors(ps,
                invokeFunc.Try().Match<Either<PowershellFailure, IEnumerable<TypedPsObject<T>>>>(
                    None: () => new PowershellFailure { Message = "Empty result" },
                    Some: r => Prelude.Right(r),
                    Fail: ex => ExceptionToPowershellFailure(ex)));


        private static PowershellFailure ExceptionToPowershellFailure(Exception ex)
        {
            return new PowershellFailure {Message = ex.Message};
        }

        private static Either<PowershellFailure, TResult> HandlePowershellErrors<TResult>(PowerShell ps, Either<PowershellFailure, TResult> result)
            => result.IsRight && ps.HadErrors
            ? new PowershellFailure {Message = ps.Streams.Error.FirstOrDefault()?.ToString()}
            : result;

        public static TryOption<IEnumerable<TypedPsObject<T>>> InvokeTyped<T>(this PowerShell ps) => 
            Prelude.TryOption(
                ps.Invoke()
                    ?.Map(r => new TypedPsObject<T>(r)));

        public static async Task<TryOption<IEnumerable<TypedPsObject<T>>>> InvokeTypedAsync<T>(this PowerShell ps) => 
            Prelude.TryOption(
                (await Task.Factory.FromAsync(ps.BeginInvoke(), ps.EndInvoke).ConfigureAwait(false))
                    ?.Map(r => new TypedPsObject<T>(r)));
    }
}
