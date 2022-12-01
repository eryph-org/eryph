using System.Threading.Tasks;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement
{
    public static class VirtualMachineInfoExtensions
    {
        public static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> StopIfRunning(
            this TypedPsObject<VirtualMachineInfo> vmInfo, IPowershellEngine engine)
        {
            if (vmInfo.Value.State == VirtualMachineState.Running ||
                vmInfo.Value.State == VirtualMachineState.RunningCritical)
                return engine.RunAsync(new PsCommandBuilder().AddCommand("Stop-VM").AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("Force")).MapAsync(u => vmInfo.Recreate());

            return Prelude.Right<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo).AsTask();
        }

        public static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Remove(
            this TypedPsObject<VirtualMachineInfo> vmInfo, IPowershellEngine engine)
        {
            
            return engine.RunAsync(new PsCommandBuilder().AddCommand("Remove-VM").AddParameter("VM", vmInfo.PsObject)
                    .AddParameter("Force"))
                .MapAsync(u => vmInfo);
        }

        public static EitherAsync<Error, TypedPsObject<T>> RecreateOrReload<T>(
            this TypedPsObject<T> vmInfo, IPowershellEngine engine)
            where T : IVirtualMachineCoreInfo
        {
            return Prelude.Try(vmInfo.Recreate().Apply(
                    r => Prelude.RightAsync<Error, TypedPsObject<T>>(r).ToEither()))
                .MatchAsync(
                    Fail: f => vmInfo.Reload(engine).ToEither(),
                    Succ: s => s).ToAsync();
        }

        public static EitherAsync<Error, TypedPsObject<T>> Reload<T>(this TypedPsObject<T> vmInfo,
            IPowershellEngine engine)
            where T : IVirtualMachineCoreInfo
        {
            return engine.GetObjectsAsync<T>(
                    new PsCommandBuilder().AddCommand("Get-VM").AddParameter("Id", vmInfo.Value.Id))
                .BindAsync(r => r.HeadOrNone()
                    .ToEither(new PowershellFailure {Message = "Failed to refresh VM data"}))
                .ToAsync().ToError()                
                ;
        }
    }
}