using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.AnsiConsole.Sys;
using LanguageExt;
using LanguageExt.Sys.Traits;
using Spectre.Console;

using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

public readonly struct SimpleConsoleRuntime :
    HasAnsiConsole<SimpleConsoleRuntime>,
    HasDirectory<SimpleConsoleRuntime>,
    HasEnvironment<SimpleConsoleRuntime>,
    HasFile<SimpleConsoleRuntime>
{
    private readonly SimpleConsoleRuntimeEnv _env;

    private SimpleConsoleRuntime(SimpleConsoleRuntimeEnv env)
    {
        _env = env;
    }

    public static SimpleConsoleRuntime New() =>
        new(new SimpleConsoleRuntimeEnv(new CancellationTokenSource()));

    public SimpleConsoleRuntime LocalCancel => new(new SimpleConsoleRuntimeEnv(new CancellationTokenSource()));

    public CancellationToken CancellationToken => _env.CancellationTokenSource.Token;

    public CancellationTokenSource CancellationTokenSource => _env.CancellationTokenSource;

    public Eff<SimpleConsoleRuntime, AnsiConsoleIO> AnsiConsoleEff => SuccessEff(LiveAnsiConsoleIO.Default);

    public Eff<SimpleConsoleRuntime, DirectoryIO> DirectoryEff => SuccessEff(LanguageExt.Sys.Live.DirectoryIO.Default);

    public Encoding Encoding => Encoding.UTF8;

    public Eff<SimpleConsoleRuntime, EnvironmentIO> EnvironmentEff => SuccessEff(LanguageExt.Sys.Live.EnvironmentIO.Default);

    public Eff<SimpleConsoleRuntime, FileIO> FileEff => SuccessEff(LanguageExt.Sys.Live.FileIO.Default);
}

public class SimpleConsoleRuntimeEnv(CancellationTokenSource cancellationTokenSource)
{
    public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;
}
