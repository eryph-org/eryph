using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Inventory;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class VirtualMachineInfoExtensions
{
    public static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> RecreateOrReload(
        this TypedPsObject<VirtualMachineInfo> vmInfo,
        IPowershellEngine engine) =>
        Try(vmInfo.Recreate).ToEither().Match(
            Left: e => vmInfo.Reload(engine),
            Right: RightAsync<Error, TypedPsObject<VirtualMachineInfo>>);
            

    public static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> Reload(
        this TypedPsObject<VirtualMachineInfo> vmInfo,
        IPowershellEngine engine) =>
        from optionalVmInfo in VmQueries.GetOptionalVmInfo(engine, vmInfo.Value.Id)
        from reloadedInfo in optionalVmInfo.ToEitherAsync(Error.New(
            $"Failed to reload data of VM {vmInfo.Value.Id}."))
        select reloadedInfo;
}
