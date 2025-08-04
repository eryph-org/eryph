using System.Runtime.Versioning;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Sys;

[SupportedOSPlatform("windows")]
public readonly struct WmiRuntime : HasWmi<WmiRuntime>
{
    public static WmiRuntime New() => new();

    public Eff<WmiRuntime, WmiIO> WmiEff => SuccessEff(LiveWmiIO.Default);
}