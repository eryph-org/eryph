using System.Threading.Tasks;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class VirtualMachineInfoExtensions
{
    /// <summary>
    /// This method aggressively stops the VM by first turning it off (hard power off in Hyper-V)
    /// and if that is not successful, it kills the VM worker process.
    /// </summary>
    public static EitherAsync<Error, Unit> StopIfRunning(
        this TypedPsObject<VirtualMachineInfo> vmInfo,
        IPowershellEngine engine) =>
        from _ in Some(vmInfo).Filter(i =>
                i.Value.State is VirtualMachineState.Running or VirtualMachineState.RunningCritical)
            .Map(i => Stop(i, engine))
            .SequenceSerial()
        select unit;

    private static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> Stop(
        this TypedPsObject<VirtualMachineInfo> vmInfo,
        IPowershellEngine engine) =>
        from _1 in RightAsync<Error , Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Stop-VM")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("TurnOff")
        from _2 in engine.RunAsync(command)
        select vmInfo;

    public static EitherAsync<Error, Unit> Remove(
        this TypedPsObject<VirtualMachineInfo> vmInfo,
        IPowershellEngine engine) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Remove-VM")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("Force")
        from _2 in engine.RunAsync(command)
        select unit;

    public static EitherAsync<Error, TypedPsObject<T>> RecreateOrReload<T>(
        this TypedPsObject<T> vmInfo,
        IPowershellEngine engine)
        where T : IVirtualMachineCoreInfo =>
        Try(vmInfo.Recreate).ToEither().Match(
            Left: e => vmInfo.Reload(engine),
            Right: RightAsync<Error, TypedPsObject<T>>);
            

    public static EitherAsync<Error, TypedPsObject<T>> Reload<T>(
        this TypedPsObject<T> vmInfo,
        IPowershellEngine engine)
        where T : IVirtualMachineCoreInfo =>
        from _  in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VM")
            .AddParameter("Id", vmInfo.Value.Id)
        from vmInfos in engine.GetObjectsAsync<T>(command)
        from reloadedInfo in vmInfos.HeadOrNone()
            .ToEitherAsync(Error.New($"Failed to reload data of VM {vmInfo.Value.Id}."))
        select reloadedInfo;
}
