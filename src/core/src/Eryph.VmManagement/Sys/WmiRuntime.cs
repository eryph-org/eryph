using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Sys;

internal readonly struct WmiRuntime : HasWmi<WmiRuntime>
{
    public static WmiRuntime New() => new();

    public Eff<WmiRuntime, WmiIO> WmiEff => SuccessEff(LiveWmiIO.Default);
}