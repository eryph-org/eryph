using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.VmManagement;
using LanguageExt.Sys.Traits;
using LanguageExt;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core.Sys;
using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero
{
    internal readonly struct DriverCommandsRuntime :
            HasConsole<DriverCommandsRuntime>,
            HasLogger<DriverCommandsRuntime>,
            HasFile<DriverCommandsRuntime>,
            HasDirectory<DriverCommandsRuntime>,
            HasEnvironment<DriverCommandsRuntime>,
            HasProcessRunner<DriverCommandsRuntime>,
            HasPowershell<DriverCommandsRuntime>,
            HasHostNetworkCommands<DriverCommandsRuntime>
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IPowershellEngine _powershellEngine;
        private readonly IHostNetworkCommands<DriverCommandsRuntime> _hostNetworkCommands
            = new HostNetworkCommands<DriverCommandsRuntime>();

        public DriverCommandsRuntime(
            CancellationTokenSource cancellationTokenSource,
            ILoggerFactory loggerFactory,
            IPowershellEngine powershellEngine)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _loggerFactory = loggerFactory;
            _powershellEngine = powershellEngine;
        }

        public DriverCommandsRuntime LocalCancel => new(new CancellationTokenSource(), _loggerFactory, _powershellEngine);

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public CancellationTokenSource CancellationTokenSource => _cancellationTokenSource;

        public Eff<DriverCommandsRuntime, ConsoleIO> ConsoleEff => SuccessEff(LanguageExt.Sys.Live.ConsoleIO.Default);

        public Encoding Encoding => Encoding.UTF8;

        public Eff<DriverCommandsRuntime, FileIO> FileEff => SuccessEff(LanguageExt.Sys.Live.FileIO.Default);

        public Eff<DriverCommandsRuntime, DirectoryIO> DirectoryEff => SuccessEff(LanguageExt.Sys.Live.DirectoryIO.Default);

        public Eff<DriverCommandsRuntime, EnvironmentIO> EnvironmentEff => SuccessEff(LanguageExt.Sys.Live.EnvironmentIO.Default);

        public Eff<DriverCommandsRuntime, ProcessRunnerIO> ProcessRunnerEff => SuccessEff(LiveProcessRunnerIO.Default);

        public Eff<DriverCommandsRuntime, IPowershellEngine> Powershell => Eff<DriverCommandsRuntime, IPowershellEngine>(rt => rt._powershellEngine);

        public Eff<DriverCommandsRuntime, ILogger> Logger(string category) => Eff<DriverCommandsRuntime, ILogger>(rt => rt._loggerFactory.CreateLogger(category));

        public Eff<DriverCommandsRuntime, ILogger<T>> Logger<T>() => Eff<DriverCommandsRuntime, ILogger<T>>(rt => rt._loggerFactory.CreateLogger<T>());

        public Eff<DriverCommandsRuntime, IHostNetworkCommands<DriverCommandsRuntime>> HostNetworkCommands =>
            Eff<DriverCommandsRuntime, IHostNetworkCommands<DriverCommandsRuntime>>(rt => rt._hostNetworkCommands);
    }
}
