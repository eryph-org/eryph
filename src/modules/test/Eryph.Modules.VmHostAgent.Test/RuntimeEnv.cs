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

public class RuntimeEnv<RT> where RT : struct, HasCancel<RT>
{
    public readonly CancellationTokenSource Source;
    public readonly CancellationToken Token;
    public readonly Encoding Encoding;
    public readonly MemoryConsole Console;
    public readonly MemoryFS FileSystem;
    public readonly TestTimeSpec TimeSpec;
    public readonly MemorySystemEnvironment SysEnv;

    public IOVSControl OVS { get; set; }
    public ISyncClient SyncClient { get; set; }

    public IHostNetworkCommands<RT> HostNetworkCommands { get; set; }
    public INetworkProviderManager NetworkProviderManager { get; set; }
    public ILoggerFactory LoggerFactory { get; set; } = new NullLoggerFactory();


    public RuntimeEnv(
        CancellationTokenSource source,
        CancellationToken token,
        Encoding encoding,
        MemoryConsole console,
        MemoryFS fileSystem,
        TestTimeSpec? timeSpec,
        MemorySystemEnvironment sysEnv)
    {
        Source = source;
        Token = token;
        Encoding = encoding;
        Console = console;
        FileSystem = fileSystem;
        TimeSpec = timeSpec ?? TestTimeSpec.RunningFromNow();
        SysEnv = sysEnv;
    }

    public RuntimeEnv(
        CancellationTokenSource source,
        Encoding encoding,
        MemoryConsole console,
        MemoryFS fileSystem,
        TestTimeSpec? timeSpec,
        MemorySystemEnvironment sysEnv) :
        this(source, source.Token, encoding, console, fileSystem, timeSpec, sysEnv)
    {
    }

    public RuntimeEnv<TestRuntime> LocalCancel =>
        new(new CancellationTokenSource(), Encoding, Console, FileSystem, TimeSpec, SysEnv);
}