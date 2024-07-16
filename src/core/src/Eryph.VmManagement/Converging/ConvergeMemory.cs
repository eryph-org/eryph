using System.Threading.Tasks;
using Eryph.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging;

public class ConvergeMemory : ConvergeTaskBase
{
    public ConvergeMemory(ConvergeContext context) : base(context)
    {
    }

    public override Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo)
        => Converge2(vmInfo).ToEither();

    private EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> Converge2(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let configuredStartupMemory = Optional(Context.Config.Memory?.Startup)
            .Filter(b => b > 0)
        let configuredMinMemory = Optional(Context.Config.Memory?.Minimum)
            .Filter(x => x > 0)
        let configuredMaxMemory = Optional(Context.Config.Memory?.Maximum)
            .Filter(x => x > 0)
        let useDynamicMemory = configuredMinMemory.IsSome || configuredMaxMemory.IsSome
        let startupMemory = configuredStartupMemory.IfNone(EryphConstants.DefaultCatletMemoryMb)
        let minMemory = configuredMinMemory.IfNone(startupMemory)
        // Apply the Hyper-V default of 1 TiB when max memory is not configured
        let maxMemory = configuredMaxMemory.IfNone(1 * 1024 * 1024)
        let changedUseDynamicMemory = Optional(useDynamicMemory)
            .Filter(d => d != vmInfo.Value.DynamicMemoryEnabled)
        let changedStartupMemory = Optional(startupMemory)
            .Filter(mb => ToBytes(mb) != vmInfo.Value.MemoryStartup)
        let changedMinMemory = useDynamicMemory
            ? Optional(minMemory).Filter(mb => ToBytes(mb) != vmInfo.Value.MemoryMinimum)
            : None
        let changedMaxMemory = useDynamicMemory 
            ? Optional(maxMemory).Filter(mb => ToBytes(mb) != vmInfo.Value.MemoryMinimum)
            : None
        let command = PsCommandBuilder.Create()
            .AddCommand("Set-VMMemory")
            .AddParameter("VM", vmInfo.PsObject)
        let command2 = changedUseDynamicMemory.Match(
            Some: d => command.AddParameter("DynamicMemoryEnabled", d),
            None: () => command) 
        let command3 = changedStartupMemory.Match(
            Some: mb => command2.AddParameter("StartupBytes", ToBytes(mb)),
            None: () => command2)
        let command4 = changedMinMemory.Match(
            Some: mb => command3.AddParameter("MinimumBytes", ToBytes(mb)),
            None: () => command3)
        let command5 = changedMaxMemory.Match(
            Some: mb => command4.AddParameter("MaximumBytes", ToBytes(mb)),
            None: () => command4)
        let message = "Updating VM memory settings: "
            + string.Join("; ",
                changedStartupMemory.Map(mb => $"Startup memory {mb} MiB").IfNone(""),
                changedUseDynamicMemory.Map(d => $"Dynamic memory {(d ? "enabled" : "disabled")}").IfNone(""),
                changedMinMemory.Map(mb => $"Minimum memory {mb} MiB").IfNone(""),
                changedMaxMemory.Map(mb => $"Maximum memory {mb} MiB").IfNone(""))
            + "."
        from result in changedUseDynamicMemory.IsSome
                       || changedStartupMemory.IsSome
                       || changedMinMemory.IsSome
                       || changedMaxMemory.IsSome
            ? from _ in TryAsync(() => Context.ReportProgress(message)).ToEither()
              from __ in Context.Engine.RunAsync(command5).ToError().ToAsync()
              from reloadedVmInfo in vmInfo.RecreateOrReload(Context.Engine)
              select reloadedVmInfo
            : vmInfo
        select vmInfo;

    private static long ToBytes (int megaBytes) => megaBytes * 1024L * 1024;
}
