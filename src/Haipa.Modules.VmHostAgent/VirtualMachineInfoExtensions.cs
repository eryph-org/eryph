using System.Threading.Tasks;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using LanguageExt;

namespace Haipa.Modules.VmHostAgent
{
    internal static class VirtualMachineInfoExtensions
    {
        public static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> StopIfRunning(this TypedPsObject<VirtualMachineInfo> vmInfo, IPowershellEngine engine)
        {
            if (vmInfo.Value.State == VirtualMachineState.Running ||
                vmInfo.Value.State == VirtualMachineState.RunningCritical)
            {
                return engine.RunAsync(new PsCommandBuilder().AddCommand("Stop-VM").AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("Force")).MapAsync(u => vmInfo.Recreate());
            }

            return Prelude.Right<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo).AsTask();
        }

        public static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Remove(this TypedPsObject<VirtualMachineInfo> vmInfo, IPowershellEngine engine)
        {
            return engine.RunAsync(new PsCommandBuilder().AddCommand("Remove-VM").AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("Force"))
                .MapAsync(u => vmInfo);
        }
    }
}