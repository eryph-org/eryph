using System.Text;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys;
using LanguageExt.Sys.Test;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eryph.Modules.GenepoolModule.Test;

public class RuntimeEnv<RT>(
    CancellationTokenSource source,
    Encoding encoding,
    MemoryConsole console,
    MemoryFS fileSystem,
    TestTimeSpec? timeSpec,
    MemorySystemEnvironment sysEnv)
    where RT : struct, HasCancel<RT>
{
    public RuntimeEnv(
        CancellationTokenSource source)
        : this(
            source,
            Encoding.Default,
            new MemoryConsole(),
            new MemoryFS(),
            TestTimeSpec.RunningFromNow(),
            MemorySystemEnvironment.InitFromSystem())
    {

    }

    public CancellationTokenSource Source { get; } = source;

    public Encoding Encoding { get; } = encoding;
    
    public MemoryConsole Console { get; } = console;
    
    public MemoryFS FileSystem { get; } = fileSystem;
    
    public TestTimeSpec TimeSpec { get; } = timeSpec ?? TestTimeSpec.RunningFromNow();
    
    public MemorySystemEnvironment SysEnv { get; } = sysEnv;

    public ILoggerFactory LoggerFactory { get; init; } = new NullLoggerFactory();
}
