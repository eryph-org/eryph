using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Core.Sys;

public static class Registry<RT> where RT : struct, HasRegistry<RT>
{
    public static Eff<RT, Option<object>> getRegistryValue(
        string keyName,
        Option<string> valueName) =>
        default(RT).RegistryEff.Map(r => Optional(r.GetValue(keyName, valueName.IfNoneUnsafe((string)null))));

    public static Eff<RT, Unit> writeRegistryValue(
        string keyName,
        Option<string> valueName,
        object value) =>
        default(RT).RegistryEff.Map(r => r.WriteValue(keyName, valueName.IfNoneUnsafe((string) null), value));
}
