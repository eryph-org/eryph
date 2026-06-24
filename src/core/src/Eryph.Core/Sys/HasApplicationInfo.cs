using LanguageExt;

namespace Eryph.Core.Sys;

public interface HasApplicationInfo<RT> where RT : struct, HasApplicationInfo<RT>
{
    Eff<RT, IApplicationInfoProvider> ApplicationInfoProviderEff { get; }
}
