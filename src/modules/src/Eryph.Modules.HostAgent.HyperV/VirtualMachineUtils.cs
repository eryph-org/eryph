using System;
using Eryph.Core;
using Eryph.Modules.HostAgent.Networks.Powershell;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Inventory;
using LanguageExt;
using LanguageExt.Effects.Traits;

using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent;

public static class VirtualMachineUtils<RT>
    where RT : struct, HasCancel<RT>, HasPowershell<RT>
{
    public static Aff<RT, TypedPsObject<VirtualMachineInfo>> getVmInfo(
        Guid guid) =>
        from powershell in default(RT).Powershell
        from ct in cancelToken<RT>()
        from vmInfo in VmQueries.GetVmInfo(powershell, guid, ct).ToAff()
        select vmInfo;

    public static Aff<RT, Option<TypedPsObject<VirtualMachineInfo>>> getOptionalVmInfo(
        Guid guid) =>
        from powershell in default(RT).Powershell
        from ct in cancelToken<RT>()
        from vmInfo in VmQueries.GetOptionalVmInfo(powershell, guid, ct).ToAff()
        select vmInfo;

    public static Aff<RT, Unit> stopVm(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from powershell in default(RT).Powershell
        from ct in cancelToken<RT>()
        let command = PsCommandBuilder.Create()
            .AddCommand("Stop-VM")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("TurnOff")
        from _ in powershell.RunAsync(command, cancellationToken: ct).ToAff()
        select unit;

    public static Aff<RT, Unit> removeVm(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from powershell in default(RT).Powershell
        let command = PsCommandBuilder.Create()
            .AddCommand("Remove-VM")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("Force")
        from _ in powershell.RunAsync(command).ToAff()
        select unit;
}
