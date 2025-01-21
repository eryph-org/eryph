using System.Threading.Tasks;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeCPU : ConvergeTaskBase
    {
        public ConvergeCPU(ConvergeContext context) : base(context)
        {
        }

        public override async Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var configCount = Context.Config.Cpu?.Count.GetValueOrDefault(1) ?? 1;
            if (vmInfo.Value.ProcessorCount == configCount) return vmInfo;

            if (vmInfo.Value.State is not (VirtualMachineState.Off or VirtualMachineState.OffCritical))
                return Error.New("Cannot change CPU count if the catlet is not stopped. Stop the catlet and retry.");

            await Context.ReportProgress($"Configure Catlet CPU count: {configCount}").ConfigureAwait(false);

            var result = await Context.Engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Set-VMProcessor")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("Count", configCount)).ConfigureAwait(false);

            if (result.IsLeft)
                return result.Map(_ => vmInfo).ToError();

            return await vmInfo.RecreateOrReload(Context.Engine).ToEither().ConfigureAwait(false);
        }
    }
}