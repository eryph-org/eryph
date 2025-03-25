using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Inventory;

public static class VmQueries
{
    public static EitherAsync<Error, Option<TypedPsObject<VirtualMachineInfo>>> GetOptionalVmInfo(
        IPowershellEngine powershellEngine,
        Guid vmId,
        CancellationToken cancellationToken = default) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VM")
            .AddParameter("Id", vmId)
        from vmInfo in powershellEngine.GetObjectAsync<VirtualMachineInfo>(command)
        select vmInfo;

    public static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> GetVmInfo(
        IPowershellEngine powershellEngine,
        Guid vmId,
        CancellationToken cancellationToken = default) =>
        from optionalVmInfo in GetOptionalVmInfo(powershellEngine, vmId)
        from vmInfo in optionalVmInfo.ToEitherAsync(
            Error.New($"The VM with ID {vmId} was not found."))
        select vmInfo;
}
