using System.Text;
using Eryph.Core;
using Eryph.Modules.VmHostAgent.Networks;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys;
using LanguageExt.Sys.Test;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eryph.Modules.VmHostAgent.Test;

public class RuntimeEnv<RT>(
    CancellationTokenSource source,
    Encoding encoding,
    MemoryConsole console,
    MemoryFS fileSystem,
    TestTimeSpec? timeSpec,
    MemorySystemEnvironment sysEnv,
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
            new MemoryConsole(),
            new MemoryFS(),
            TestTimeSpec.RunningFromNow(),
            MemorySystemEnvironment.InitFromSystem(),
            ovsControl,
            syncClient,
            hostNetworkCommands,
            networkProviderManager)
    {

    }

    public CancellationTokenSource Source { get; } = source;

    public Encoding Encoding { get; } = encoding;
    
    public MemoryConsole Console { get; } = console;
    
    public MemoryFS FileSystem { get; } = fileSystem;
    
    public TestTimeSpec TimeSpec { get; } = timeSpec ?? TestTimeSpec.RunningFromNow();
    
    public MemorySystemEnvironment SysEnv { get; } = sysEnv;

    public IOVSControl OVSControl { get; init; } = ovsControl;

    public ISyncClient SyncClient { get; init; } = syncClient;

    public IHostNetworkCommands<RT> HostNetworkCommands { get; init; } = hostNetworkCommands;

    public INetworkProviderManager NetworkProviderManager { get; init; } = networkProviderManager;

    public ILoggerFactory LoggerFactory { get; init; } = new NullLoggerFactory();
}
