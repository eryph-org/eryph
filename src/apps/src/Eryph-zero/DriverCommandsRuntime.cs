using Eryph.Modules.HostAgent.Networks.Powershell;
using Eryph.Modules.HostAgent.Networks;
using Eryph.VmManagement;
using LanguageExt.Sys.Traits;
using LanguageExt;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Eryph.AnsiConsole.Sys;
using Eryph.Core.Sys;
using Eryph.VmManagement.Sys;
using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

internal readonly struct DriverCommandsRuntime :
    HasAnsiConsole<DriverCommandsRuntime>,
    HasDirectory<DriverCommandsRuntime>,
    HasDism<DriverCommandsRuntime>,
    HasFile<DriverCommandsRuntime>,
    HasHostNetworkCommands<DriverCommandsRuntime>,
    HasLogger<DriverCommandsRuntime>,
    HasProcessRunner<DriverCommandsRuntime>,
    HasPowershell<DriverCommandsRuntime>,
    HasRegistry<DriverCommandsRuntime>,
    HasWmi<DriverCommandsRuntime>
{
    public DriverCommandsRuntime(DriverCommandsRuntimeEnv env)
    {
        Env = env;
    }

    public DriverCommandsRuntimeEnv Env { get; }

    public DriverCommandsRuntime LocalCancel => new(new(
        new CancellationTokenSource(),
        Env.LoggerFactory,
        Env.PowershellEngine));

    public Eff<DriverCommandsRuntime, AnsiConsoleIO> AnsiConsoleEff => SuccessEff(LiveAnsiConsoleIO.Default);

    public CancellationToken CancellationToken => Env.CancellationTokenSource.Token;

    public CancellationTokenSource CancellationTokenSource => Env.CancellationTokenSource;

    public Eff<DriverCommandsRuntime, ConsoleIO> ConsoleEff => SuccessEff(LanguageExt.Sys.Live.ConsoleIO.Default);

    public Eff<DriverCommandsRuntime, DirectoryIO> DirectoryEff => SuccessEff(LanguageExt.Sys.Live.DirectoryIO.Default);

    public Eff<DriverCommandsRuntime, DismIO> DismEff => SuccessEff(LiveDismIO.Default);

    public Encoding Encoding => Encoding.UTF8;

    public Eff<DriverCommandsRuntime, FileIO> FileEff => SuccessEff(LanguageExt.Sys.Live.FileIO.Default);

    public Eff<DriverCommandsRuntime, ProcessRunnerIO> ProcessRunnerEff => SuccessEff(LiveProcessRunnerIO.Default);

    public Eff<DriverCommandsRuntime, IPowershellEngine> Powershell =>
        Eff<DriverCommandsRuntime, IPowershellEngine>(rt => rt.Env.PowershellEngine);

    public Eff<DriverCommandsRuntime, ILogger> Logger(string category) =>
        Eff<DriverCommandsRuntime, ILogger>(rt => rt.Env.LoggerFactory.CreateLogger(category));

    public Eff<DriverCommandsRuntime, ILogger<T>> Logger<T>() =>
        Eff<DriverCommandsRuntime, ILogger<T>>(rt => rt.Env.LoggerFactory.CreateLogger<T>());

    public Eff<DriverCommandsRuntime, IHostNetworkCommands<DriverCommandsRuntime>> HostNetworkCommands =>
        Eff<DriverCommandsRuntime, IHostNetworkCommands<DriverCommandsRuntime>>(rt => rt.Env.HostNetworkCommands);

    public Eff<DriverCommandsRuntime, RegistryIO> RegistryEff => SuccessEff(LiveRegistryIO.Default);

    public Eff<DriverCommandsRuntime, WmiIO> WmiEff => SuccessEff(LiveWmiIO.Default);
}

internal class DriverCommandsRuntimeEnv
{
    public DriverCommandsRuntimeEnv(
        CancellationTokenSource cancellationTokenSource,
        ILoggerFactory loggerFactory,
        IPowershellEngine powershellEngine)
    {
        CancellationTokenSource = cancellationTokenSource;
        LoggerFactory = loggerFactory;
        PowershellEngine = powershellEngine;
    }

    public CancellationTokenSource CancellationTokenSource { get; }

    public IHostNetworkCommands<DriverCommandsRuntime> HostNetworkCommands { get; } = new HostNetworkCommands<DriverCommandsRuntime>();

    public ILoggerFactory LoggerFactory { get; }

    public IPowershellEngine PowershellEngine { get; }
}
