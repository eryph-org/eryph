using Eryph.VmManagement.Data.Core;
using LanguageExt;

namespace Eryph.VmManagement.Sys;

public static class Dism<RT> where RT : struct, HasDism<RT>
{
    public static Aff<RT, Seq<DismDriverInfo>> getInstalledDriverPackages() =>
        default(RT).DismEff.MapAsync(e => e.GetInstalledDriverPackages());
}
