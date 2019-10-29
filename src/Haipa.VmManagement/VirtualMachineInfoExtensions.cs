using System;
using System.Threading.Tasks;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using LanguageExt;

namespace Haipa.Modules.VmHostAgent
{
    public static class VirtualMachineInfoExtensions
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

        public static Task<Either<PowershellFailure, TypedPsObject<T>>> RecreateOrReload<T>(this TypedPsObject<T> vmInfo, IPowershellEngine engine) 
            where T: IVirtualMachineCoreInfo
        {
            return Prelude.Try(vmInfo.Recreate().Apply(
                    r => Prelude.RightAsync<PowershellFailure, TypedPsObject<T>>(r).ToEither()))
                .MatchAsync(
                    Fail: f => vmInfo.Reload(engine),
                    Succ: s => s);

        }

        public static Task<Either<PowershellFailure, TypedPsObject<T>>> Reload<T>(this TypedPsObject<T> vmInfo, IPowershellEngine engine)
        where T: IVirtualMachineCoreInfo
        {
            return engine.GetObjectsAsync<T>(
                    new PsCommandBuilder().AddCommand("Get-VM").AddParameter("Id", vmInfo.Value.Id))
                .BindAsync(r => r.HeadOrNone().ToEither(new PowershellFailure {Message = "Failed to refresh VM data"}));


        }
    }
}