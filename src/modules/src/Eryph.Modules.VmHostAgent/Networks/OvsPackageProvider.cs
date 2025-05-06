using Eryph.Core.Sys;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement.Sys;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys.Traits;
using LanguageExt;
using LanguageExt.Sys.IO;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks;

public static class OvsPackageProvider<RT> where RT : struct,
    HasCancel<RT>,
    HasDism<RT>,
    HasFile<RT>,
    HasHostNetworkCommands<RT>,
    HasLogger<RT>,
    HasPowershell<RT>,
    HasProcessRunner<RT>,
    HasRegistry<RT>
{
    public static Aff<RT, string> ensureOvsDirectory(string ovsPackagePath) =>
        from _ in SuccessAff(unit)
        select "";
}
