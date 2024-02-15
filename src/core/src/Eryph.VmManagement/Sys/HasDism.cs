using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Eryph.VmManagement.Sys;

public interface HasDism<RT> : HasCancel<RT>
    where RT : struct, HasDism<RT>, HasCancel<RT>
{
    Eff<RT, DismIO> DismEff { get; }
}
