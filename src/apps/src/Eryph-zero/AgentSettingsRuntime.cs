using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.VmManagement.Sys;
using LanguageExt;
using LanguageExt.Sys.Traits;

using static LanguageExt.Prelude;

namespace Eryph.Runtime.Zero;

internal readonly struct AgentSettingsRuntime :
    HasConsole<AgentSettingsRuntime>,
    HasDirectory<AgentSettingsRuntime>,
    HasFile<AgentSettingsRuntime>,
    HasWmi<AgentSettingsRuntime>
{
    private readonly AgentSettingsRuntimeEnv _env;

    private AgentSettingsRuntime(AgentSettingsRuntimeEnv env)
    {
        _env = env;
    }

    public static AgentSettingsRuntime New()
    {
        return new AgentSettingsRuntime(new AgentSettingsRuntimeEnv(new CancellationTokenSource()));
    }

    public AgentSettingsRuntime LocalCancel => new(new AgentSettingsRuntimeEnv(new CancellationTokenSource()));

    public CancellationToken CancellationToken => _env.CancellationTokenSource.Token;

    public CancellationTokenSource CancellationTokenSource => _env.CancellationTokenSource;
    
    public Encoding Encoding => Encoding.UTF8;

    public Eff<AgentSettingsRuntime, ConsoleIO> ConsoleEff => SuccessEff(LanguageExt.Sys.Live.ConsoleIO.Default);

    public Eff<AgentSettingsRuntime, DirectoryIO> DirectoryEff => SuccessEff(LanguageExt.Sys.Live.DirectoryIO.Default);

    public Eff<AgentSettingsRuntime, FileIO> FileEff => SuccessEff(LanguageExt.Sys.Live.FileIO.Default);

    public Eff<AgentSettingsRuntime, WmiIO> WmiEff => SuccessEff(LiveWmiIO.Default);
}

internal class AgentSettingsRuntimeEnv(CancellationTokenSource tokenSource)
{
    public CancellationTokenSource CancellationTokenSource { get; } = tokenSource;
}