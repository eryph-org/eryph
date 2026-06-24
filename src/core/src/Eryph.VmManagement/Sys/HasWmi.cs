using LanguageExt;

namespace Eryph.VmManagement.Sys;

public interface HasWmi<RT> where RT : struct, HasWmi<RT>
{
    Eff<RT, WmiIO> WmiEff { get; }
}
