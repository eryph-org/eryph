using LanguageExt;

namespace Eryph.Core.Sys;

public interface HasRegistry<RT>
    where RT : struct, HasRegistry<RT>
{
    Eff<RT, RegistryIO> RegistryEff { get; }
}
