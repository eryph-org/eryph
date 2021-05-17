using System.Threading.Tasks;
using Haipa.VmManagement.Data.Full;
using LanguageExt;

namespace Haipa.VmManagement.Converging
{
    public class ConvergeCPU : ConvergeTaskBase
    {
        public ConvergeCPU(ConvergeContext context) : base(context)
        {
        }

        public override async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Converge(TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var configCount = Context.Config.VM.Cpu.Count.GetValueOrDefault(1);
            if (vmInfo.Value.ProcessorCount == configCount) return vmInfo;
            
            await Context.ReportProgress($"Configure VM Processor: Count: {configCount}").ConfigureAwait(false);

            await Context.Engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Set-VMProcessor")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("Count", configCount)).ConfigureAwait(false);

            return await vmInfo.RecreateOrReload(Context.Engine).ConfigureAwait(false);

        }


    }
}