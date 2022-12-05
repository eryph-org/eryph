using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Sys.Traits;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace PowershellStandalone;

public readonly struct ConsoleRuntime : 
    HasPowershell<ConsoleRuntime>, 
    HasConsole<ConsoleRuntime>,
    HasHostNetworkCommands<ConsoleRuntime>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPowershellEngine _engine;

    public ConsoleRuntime(
        ILoggerFactory loggerFactory,
        IPowershellEngine engine,
        CancellationTokenSource cancellationTokenSource)
    {
        _loggerFactory = loggerFactory;
        _engine = engine;
        CancellationTokenSource = cancellationTokenSource;
    }

    public Eff<ConsoleRuntime, IPowershellEngine> Powershell =>
        Eff<ConsoleRuntime, IPowershellEngine>(rt => rt._engine);

    public Eff<ConsoleRuntime, IHostNetworkCommands<ConsoleRuntime>> HostNetworkCommands =>
        SuccessEff<IHostNetworkCommands<ConsoleRuntime>>(
            new HostNetworkCommands<ConsoleRuntime>());

    public ConsoleRuntime LocalCancel => new(_loggerFactory,_engine, CancellationTokenSource);

    public CancellationToken CancellationToken => CancellationTokenSource.Token;
    public CancellationTokenSource CancellationTokenSource { get; }

    public Eff<ConsoleRuntime,ConsoleIO> ConsoleEff =>
        SuccessEff(LanguageExt.Sys.Live.ConsoleIO.Default);

    public Eff<ConsoleRuntime, ILogger> Logger(string category) => 
        Eff<ConsoleRuntime, ILogger>(rt => rt._loggerFactory.CreateLogger(category));

    public Eff<ConsoleRuntime, ILogger<T>> Logger<T>() =>
        Eff<ConsoleRuntime, ILogger<T>>(rt => rt._loggerFactory.CreateLogger<T>());

}