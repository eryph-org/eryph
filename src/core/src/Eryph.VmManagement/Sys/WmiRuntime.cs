using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Effects.Traits;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Sys;

public readonly struct WmiRuntime :
    HasCancel<WmiRuntime>,
    HasWmi<WmiRuntime>
{
    private readonly WmiRuntimeEnv _env;

    private WmiRuntime(WmiRuntimeEnv env)
    {
        _env = env;
    }

    public static WmiRuntime New() => new(new WmiRuntimeEnv(new CancellationTokenSource()));

    public WmiRuntime LocalCancel => new(new WmiRuntimeEnv(new CancellationTokenSource()));

    public CancellationToken CancellationToken => _env.Source.Token;

    public CancellationTokenSource CancellationTokenSource => _env.Source;

    public Eff<WmiRuntime, WmiIO> WmiEff => SuccessEff(LiveWmiIO.Default);
}

public class WmiRuntimeEnv(CancellationTokenSource source)
{
    public CancellationTokenSource Source { get; } = source;
}
