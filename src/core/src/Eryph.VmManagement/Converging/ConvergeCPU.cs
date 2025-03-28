using System.Threading.Tasks;
using Eryph.Core;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging;

public class ConvergeCPU(
    ConvergeContext context)
    : ConvergeTaskBase(context)
{
    public override Task<Either<Error, Unit>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        ConvergeCpu(vmInfo).ToEither();

    private EitherAsync<Error, Unit> ConvergeCpu(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let expectedCpuCount = Context.Config.Cpu?.Count ?? EryphConstants.DefaultCatletCpuCount
        let currentCpuCount = vmInfo.Value.ProcessorCount
        from _2 in expectedCpuCount == currentCpuCount
            ? RightAsync<Error, Unit>(unit)
            : ConfigureCpu(vmInfo, expectedCpuCount)
        select unit;

    private EitherAsync<Error, Unit> ConfigureCpu(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        int cpuCount) =>
        from _1 in guard(vmInfo.Value.State is VirtualMachineState.Off or VirtualMachineState.OffCritical,
                Error.New("Cannot change CPU count if the catlet is not stopped. Stop the catlet and retry."))
            .ToEitherAsync()
        from _2 in Context.ReportProgressAsync($"Configure Catlet CPU count: {cpuCount}")
        let command = PsCommandBuilder.Create()
            .AddCommand("Set-VMProcessor")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("Count", cpuCount)
        from _3 in Context.Engine.RunAsync(command)
        select unit;
}
