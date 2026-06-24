using LanguageExt;

namespace Eryph.Core.Sys;

public static class ApplicationInfo<RT>
    where RT : struct, HasApplicationInfo<RT>
{
    public static Eff<RT, string> applicationId() =>
        default(RT).ApplicationInfoProviderEff.Map(p => p.ApplicationId);
}
