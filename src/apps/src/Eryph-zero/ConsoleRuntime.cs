using System;
using System.Text;
using System.Threading;
using Eryph.AnsiConsole.Sys;
using Eryph.Core;
using Eryph.Core.Sys;
using Eryph.Modules.HostAgent;
using Eryph.Modules.HostAgent.Networks;
using Eryph.Modules.HostAgent.Networks.OVS;
using Eryph.Modules.HostAgent.Networks.Powershell;
using Eryph.Runtime.Zero.Configuration.Networks;
using Eryph.VmManagement;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Sys.Traits;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

public readonly struct ConsoleRuntime(ConsoleRuntimeEnv env) :
    HasAnsiConsole<ConsoleRuntime>,
    HasPowershell<ConsoleRuntime>,
    HasOVSControl<ConsoleRuntime>,
    HasAgentSyncClient<ConsoleRuntime>,
    HasHostNetworkCommands<ConsoleRuntime>,
    HasConsole<ConsoleRuntime>,
    HasNetworkProviderManager<ConsoleRuntime>,
    HasLogger<ConsoleRuntime>,
    HasFile<ConsoleRuntime>,
    HasProcessRunner<ConsoleRuntime>,
    HasRegistry<ConsoleRuntime>,
    HasDism<ConsoleRuntime>
{
    private readonly ConsoleRuntimeEnv? _env = env;

    public ConsoleRuntimeEnv Env => _env ?? throw new InvalidOperationException("Runtime env is not set");

    public Eff<ConsoleRuntime, IPowershellEngine> Powershell =>
        Eff<ConsoleRuntime, IPowershellEngine>(rt => rt.Env.PowershellEngine);

    public Eff<ConsoleRuntime, IOVSControl> OVS =>
        Eff<ConsoleRuntime, IOVSControl>(rt => new OVSControl(rt.Env.SystemEnvironment));

    public Eff<ConsoleRuntime, IHostNetworkCommands<ConsoleRuntime>> HostNetworkCommands =>
        SuccessEff<IHostNetworkCommands<ConsoleRuntime>>(
            new HostNetworkCommands<ConsoleRuntime>());

    public ConsoleRuntime LocalCancel => new(new ConsoleRuntimeEnv(
        Env.LoggerFactory, Env.PowershellEngine, Env.SystemEnvironment,
        new CancellationTokenSource()));

    public CancellationToken CancellationToken => Env.Token;
    
    public CancellationTokenSource CancellationTokenSource => Env.TokenSource;

    public Eff<ConsoleRuntime, ISyncClient> AgentSync =>
        Eff<ConsoleRuntime, ISyncClient>(_ => new SyncClient());

    public Eff<ConsoleRuntime,ConsoleIO> ConsoleEff =>
        SuccessEff(LanguageExt.Sys.Live.ConsoleIO.Default);

    public Eff<ConsoleRuntime, INetworkProviderManager> NetworkProviderManager =>
        Eff<ConsoleRuntime, INetworkProviderManager>(_ => new NetworkProviderManager());

    public Eff<ConsoleRuntime, ILogger> Logger(string category) => 
        Eff<ConsoleRuntime, ILogger>(rt => rt.Env.LoggerFactory.CreateLogger(category));

    public Eff<ConsoleRuntime, ILogger<T>> Logger<T>() =>
        Eff<ConsoleRuntime, ILogger<T>>(rt => rt.Env.LoggerFactory.CreateLogger<T>());

    public Encoding Encoding => Encoding.UTF8;

    public Eff<ConsoleRuntime, FileIO> FileEff => SuccessEff(LanguageExt.Sys.Live.FileIO.Default);

    public Eff<ConsoleRuntime, ProcessRunnerIO> ProcessRunnerEff => SuccessEff(LiveProcessRunnerIO.Default);

    public Eff<ConsoleRuntime, RegistryIO> RegistryEff => SuccessEff(LiveRegistryIO.Default);

    public Eff<ConsoleRuntime, DismIO> DismEff => SuccessEff(LiveDismIO.Default);

    public Eff<ConsoleRuntime, AnsiConsoleIO> AnsiConsoleEff => SuccessEff(LiveAnsiConsoleIO.Default);
}
