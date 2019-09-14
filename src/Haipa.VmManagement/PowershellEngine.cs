using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using LanguageExt;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Haipa.VmManagement
{
    public interface IPowershellEngine
    {
        Either<PowershellFailure, Seq<TypedPsObject<T>>> GetObjects<T>(PsCommandBuilder builder, Action<int> reportProgress = null);
        Either<PowershellFailure, Unit> Run(PsCommandBuilder builder,  Action<int> reportProgress = null);
        Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetObjectsAsync<T>(PsCommandBuilder builder, Func<int, Task> reportProgress = null);
        Task<Either<PowershellFailure, Unit>> RunAsync(PsCommandBuilder builder, Func<int, Task> reportProgress = null);

    }

    public class PowershellEngine :  IPowershellEngine, IDisposable
    {
        private readonly RunspacePool _runspace;

        public PowershellEngine()
        {
            var iss = InitialSessionState.CreateDefault2();
            _runspace = RunspaceFactory.CreateRunspacePool(iss);
            _runspace.Open();

            using (var ps = CreateShell())
            {
                ps.AddScript("import-module Hyper-V -RequiredVersion 2.0");
                ps.Invoke();
            }

        }

        public PowerShell CreateShell()
        {

            var ps = PowerShell.Create();
            ps.RunspacePool = _runspace;

            return ps;
        }

        public Either<PowershellFailure, Seq<TypedPsObject<T>>> GetObjects<T>(PsCommandBuilder builder, Action<int> reportProgress= null)
        {
            using (var ps = CreateShell())
            {
                builder.Build(ps);

                InitializeProgressReporting(ps, reportProgress);
                return ps.GetObjects<T>();
            }
        }

        public async Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetObjectsAsync<T>(PsCommandBuilder builder, Func<int, Task> reportProgress = null)
        {
            using (var ps = CreateShell())
            {
                builder.Build(ps);
                InitializeAsyncProgressReporting(ps, reportProgress);

                return await ps.GetObjectsAsync<T>().ConfigureAwait(false);
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

        public void Dispose()
        {
            _runspace?.Dispose();
        }
    }

    public class PsCommandBuilder
    {
        public static PsCommandBuilder Create()
        {
            return new PsCommandBuilder();
        }

        private readonly List<Tuple<DataType,object,string>> _dataChain = new List<Tuple<DataType, object, string>>();


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

        public PsCommandBuilder AddParameter(string parameter)
        {
            _dataChain.Add(new Tuple<DataType, object, string>(DataType.SwitchParameter, null,parameter));
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
            SwitchParameter,
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
                    case DataType.SwitchParameter:
                        ps.AddParameter(data.Item3);
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


    internal static class PowerShellInvokeExtensions
    {

        public static Either<PowershellFailure, Seq<TypedPsObject<T>>> GetObjects<T>(this PowerShell ps) =>
            InvokeGetObjects(ps, ps.InvokeTyped<T>());

        public static async Task<Either<PowershellFailure, Seq<TypedPsObject<T>>>> GetObjectsAsync<T>(this PowerShell ps)
        {
            var tryResult = await
                    Prelude.TryAsync(Task.Factory.FromAsync(ps.BeginInvoke(), ps.EndInvoke)).Try().ConfigureAwait(false);

            var result = tryResult.Match<Either<PowershellFailure, Seq<TypedPsObject<T>>>>(
                    Succ: s => s.Map(x => new TypedPsObject<T>(x)).ToSeq(),
                    Fail: ex => ExceptionToPowershellFailure(ex));
      
            return HandlePowershellErrors(ps, result);
        }

        public static Either<PowershellFailure, Unit> Run(this PowerShell ps) =>
            HandlePowershellErrors(ps,
                Prelude.Try(ps.Invoke).Try().Match<Either<PowershellFailure, Unit>>(
                    Succ: s => Unit.Default,
                    Fail: ex => ExceptionToPowershellFailure(ex)));

        public static async Task<Either<PowershellFailure, Unit>> RunAsync(this PowerShell ps)
        {
            var tryResult = await
                Prelude.TryAsync(Task.Factory.FromAsync(ps.BeginInvoke(), ps.EndInvoke)).Try().ConfigureAwait(false);

            var result = tryResult.Match<Either<PowershellFailure, Unit>>(
                    Succ: s => Unit.Default,
                    Fail: ex => ExceptionToPowershellFailure(ex));

            return HandlePowershellErrors(ps, result);
        }

        private static Either<PowershellFailure, Seq<TypedPsObject<T>>> InvokeGetObjects<T>(this PowerShell ps,
            Try<Seq<TypedPsObject<T>>> invokeFunc) =>
            HandlePowershellErrors(ps, invokeFunc.ToEither(ExceptionToPowershellFailure));


        private static PowershellFailure ExceptionToPowershellFailure(Exception ex)
        {
            return new PowershellFailure {Message = ex.Message};
        }

        private static Either<PowershellFailure, TResult> HandlePowershellErrors<TResult>(PowerShell ps, Either<PowershellFailure, TResult> result)
            => result.IsRight && ps.Streams.Error.Count > 0
            ? new PowershellFailure {Message = ps.Streams.Error.FirstOrDefault()?.ToString()}
            : result;

        public static Try<Seq<TypedPsObject<T>>> InvokeTyped<T>(this PowerShell ps) => 
            Prelude.Try(ps.Invoke().Map(x=>new TypedPsObject<T>(x)).ToSeq());
        
    }
}
