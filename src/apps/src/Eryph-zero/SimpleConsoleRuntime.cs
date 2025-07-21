using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.AnsiConsole.Sys;
using Eryph.Core;
using Eryph.Core.Sys;
using Eryph.Modules.Genepool.Inventory;
using Eryph.Modules.VmHostAgent;
using Eryph.Modules.VmHostAgent.Inventory;
using Eryph.VmManagement;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Sys.Traits;
using Microsoft.Extensions.Logging.Abstractions;

using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

public readonly struct SimpleConsoleRuntime :
    HasAnsiConsole<SimpleConsoleRuntime>,
    HasApplicationInfo<SimpleConsoleRuntime>,
    HasDirectory<SimpleConsoleRuntime>,
    HasEnvironment<SimpleConsoleRuntime>,
    HasFile<SimpleConsoleRuntime>,
    HasHardwareId<SimpleConsoleRuntime>,
    HasWmi<SimpleConsoleRuntime>
{
    private readonly SimpleConsoleRuntimeEnv _env;

    private SimpleConsoleRuntime(SimpleConsoleRuntimeEnv env)
    {
        _env = env;
    }

    public static SimpleConsoleRuntime New() =>
        new(new SimpleConsoleRuntimeEnv(
            new ZeroApplicationInfoProvider(),
            new WindowsHardwareIdProvider(new NullLoggerFactory()),
            new SyncClient(),
            new CancellationTokenSource()));

    public SimpleConsoleRuntime LocalCancel => new(new SimpleConsoleRuntimeEnv(
        _env.ApplicationInfoProvider,
        _env.HardwareIdProvider,
        _env.SyncClient,
        new CancellationTokenSource()));

    public CancellationToken CancellationToken => _env.CancellationTokenSource.Token;

    public CancellationTokenSource CancellationTokenSource => _env.CancellationTokenSource;

    public Eff<SimpleConsoleRuntime, AnsiConsoleIO> AnsiConsoleEff => SuccessEff(LiveAnsiConsoleIO.Default);

    public Eff<SimpleConsoleRuntime, IApplicationInfoProvider> ApplicationInfoProviderEff =>
        Eff<SimpleConsoleRuntime, IApplicationInfoProvider>(rt => rt._env.ApplicationInfoProvider);

    public Eff<SimpleConsoleRuntime, DirectoryIO> DirectoryEff => SuccessEff(LanguageExt.Sys.Live.DirectoryIO.Default);

    public Encoding Encoding => Encoding.UTF8;

    public Eff<SimpleConsoleRuntime, EnvironmentIO> EnvironmentEff => SuccessEff(LanguageExt.Sys.Live.EnvironmentIO.Default);

    public Eff<SimpleConsoleRuntime, FileIO> FileEff => SuccessEff(LanguageExt.Sys.Live.FileIO.Default);

    public Eff<SimpleConsoleRuntime, IHardwareIdProvider> HardwareIdProviderEff =>
        Eff<SimpleConsoleRuntime, IHardwareIdProvider>(rt => rt._env.HardwareIdProvider);

    public Eff<SimpleConsoleRuntime, ISyncClient> SyncClientEff =>
        Eff<SimpleConsoleRuntime, ISyncClient>(rt => rt._env.SyncClient);

    public Eff<SimpleConsoleRuntime, WmiIO> WmiEff => SuccessEff(LiveWmiIO.Default);
}

public class SimpleConsoleRuntimeEnv(
    IApplicationInfoProvider applicationInfoProvider,
    IHardwareIdProvider hardwareIdProvider,
    ISyncClient syncClient,
    CancellationTokenSource cancellationTokenSource)
{
    public IApplicationInfoProvider ApplicationInfoProvider { get; } = applicationInfoProvider;

    public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;

    public IHardwareIdProvider HardwareIdProvider { get; } = hardwareIdProvider;

    public ISyncClient SyncClient { get; } = syncClient;
}
