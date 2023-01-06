using System.Threading;
using Dbosoft.OVN;
using Eryph.Core;
using Eryph.Modules.VmHostAgent;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.Runtime.Zero.Configuration.Networks;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Sys.Traits;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

public readonly struct ConsoleRuntime : 
    HasPowershell<ConsoleRuntime>, 
    HasOVSControl<ConsoleRuntime>,
    HasAgentSyncClient<ConsoleRuntime>,
    HasHostNetworkCommands<ConsoleRuntime>,
    HasConsole<ConsoleRuntime>,
    HasNetworkProviderManager<ConsoleRuntime>,
    HasLogger<ConsoleRuntime>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPowershellEngine _engine;
    private readonly ISysEnvironment _sysEnv;

    public ConsoleRuntime(
        ILoggerFactory loggerFactory,
        IPowershellEngine engine,
        ISysEnvironment sysEnv,
        CancellationTokenSource cancellationTokenSource)
    {
        _loggerFactory = loggerFactory;
        _engine = engine;
        CancellationTokenSource = cancellationTokenSource;
        _sysEnv = sysEnv;
    }

    public Eff<ConsoleRuntime, IPowershellEngine> Powershell =>
        Eff<ConsoleRuntime, IPowershellEngine>(rt => rt._engine);

    public Eff<ConsoleRuntime, IOVSControl> OVS =>
        Eff<ConsoleRuntime, IOVSControl>(rt => new OVSControl(rt._sysEnv));

    public Eff<ConsoleRuntime, IHostNetworkCommands<ConsoleRuntime>> HostNetworkCommands =>
        SuccessEff<IHostNetworkCommands<ConsoleRuntime>>(
            new HostNetworkCommands<ConsoleRuntime>());

    public ConsoleRuntime LocalCancel => new(_loggerFactory,_engine, _sysEnv, CancellationTokenSource);

    public CancellationToken CancellationToken => CancellationTokenSource.Token;
    public CancellationTokenSource CancellationTokenSource { get; }
    public Eff<ConsoleRuntime, ISyncClient> AgentSync =>
        Eff<ConsoleRuntime, ISyncClient>(rt => new SyncClient());

    public Eff<ConsoleRuntime,ConsoleIO> ConsoleEff =>
        SuccessEff(LanguageExt.Sys.Live.ConsoleIO.Default);

    public Eff<ConsoleRuntime, INetworkProviderManager> NetworkProviderManager =>
        Eff<ConsoleRuntime, INetworkProviderManager>(rt => new NetworkProviderManager());

    public Eff<ConsoleRuntime, ILogger> Logger(string category) => 
        Eff<ConsoleRuntime, ILogger>(rt => rt._loggerFactory.CreateLogger(category));

    public Eff<ConsoleRuntime, ILogger<T>> Logger<T>() =>
        Eff<ConsoleRuntime, ILogger<T>>(rt => rt._loggerFactory.CreateLogger<T>());

}