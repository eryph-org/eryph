using System.Text;
using Eryph.Core;
using Eryph.Modules.HostAgent;
using Eryph.Modules.HostAgent.Networks;
using Eryph.Modules.HostAgent.Networks.OVS;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys;
using LanguageExt.Sys.Test;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Eryph.Modules.HostAgent.HyperV.Test;

public class RuntimeEnv<RT>(
    CancellationTokenSource source,
    Encoding encoding,
    MemoryFS fileSystem,
    TestTimeSpec? timeSpec,
    MemorySystemEnvironment sysEnv,
    TestConsole ansiConsole,
    IOVSControl ovsControl,
    ISyncClient syncClient,
    IHostNetworkCommands<RT> hostNetworkCommands,
    INetworkProviderManager networkProviderManager)
    where RT : struct, HasCancel<RT>
{
    public RuntimeEnv(
        CancellationTokenSource source,
        IOVSControl ovsControl,
        ISyncClient syncClient,
        IHostNetworkCommands<RT> hostNetworkCommands,
        INetworkProviderManager networkProviderManager)
        : this(
            source,
            Encoding.Default,
            new MemoryFS(),
            TestTimeSpec.RunningFromNow(),
            MemorySystemEnvironment.InitFromSystem(),
            new TestConsole(),
            ovsControl,
            syncClient,
            hostNetworkCommands,
            networkProviderManager)
    {

    }

    public CancellationTokenSource Source { get; } = source;

    public Encoding Encoding { get; } = encoding;
    
    public MemoryFS FileSystem { get; } = fileSystem;
    
    public TestTimeSpec TimeSpec { get; } = timeSpec ?? TestTimeSpec.RunningFromNow();
    
    public MemorySystemEnvironment SysEnv { get; } = sysEnv;

    public TestConsole AnsiConsole { get; } = ansiConsole;

    public IOVSControl OVSControl { get; init; } = ovsControl;

    public ISyncClient SyncClient { get; init; } = syncClient;

    public IHostNetworkCommands<RT> HostNetworkCommands { get; init; } = hostNetworkCommands;

    public INetworkProviderManager NetworkProviderManager { get; init; } = networkProviderManager;

    public ILoggerFactory LoggerFactory { get; init; } = new NullLoggerFactory();
}
