using System;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging;

public class ConvergeMemory(ConvergeContext context) : ConvergeTaskBase(context)
{
    public override Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        Converge(Context.Config, vmInfo, Context.Engine, Context.ReportProgress)
            .ToEither();

    private static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> Converge(
        CatletConfig config,
        TypedPsObject<VirtualMachineInfo> vmInfo,
        IPowershellEngine powershellEngine,
        Func<string, Task> reportProgress) =>
        from configuredMemory in ValidateMemoryConfig(config.Memory)
            .ToAsync()
        let capabilities = config.Capabilities.ToSeq()
        let isDynamicMemoryEnabled = CatletCapabilities.IsDynamicMemoryEnabled(capabilities)
        let isDynamicMemoryExplicitlyDisabled = CatletCapabilities
            .IsDynamicMemoryExplicitlyDisabled(capabilities)
        let useDynamicMemory = !isDynamicMemoryExplicitlyDisabled
            && (isDynamicMemoryEnabled || configuredMemory.Minimum.IsSome || configuredMemory.Maximum.IsSome)
        // When startup, minimum and maximum are not all configured, we ensure
        // that the missing value are consistent with the configured ones.
        let minMemory = configuredMemory.Minimum
                        | configuredMemory.Startup.Map(s => Math.Min(s, vmInfo.Value.MemoryMinimum))
                        | configuredMemory.Maximum.Map(max => Math.Min(max, vmInfo.Value.MemoryMinimum))
        let maxMemory = configuredMemory.Maximum
                        | configuredMemory.Startup.Map(s => Math.Max(s, vmInfo.Value.MemoryMaximum))
                        | configuredMemory.Minimum.Map(min => Math.Max(min, vmInfo.Value.MemoryMaximum))
        let startupMemory = configuredMemory.Startup
                            | configuredMemory.Minimum.Map(min => Math.Max(min, vmInfo.Value.MemoryStartup))
                            | configuredMemory.Maximum.Map(max => Math.Min(max, vmInfo.Value.MemoryStartup))
        // We now identify the values which must actually be changed
        // compared to the current state of the VM. Then, we create
        // the PowerShell command to apply the changes.
        let changedUseDynamicMemory = Optional(useDynamicMemory)
            .Filter(d => d != vmInfo.Value.DynamicMemoryEnabled)
        let changedStartupMemory = startupMemory
            .Filter(b => b != vmInfo.Value.MemoryStartup)
        let changedMinMemory = useDynamicMemory
            ? minMemory.Filter(b => b != vmInfo.Value.MemoryMinimum)
            : None
        let changedMaxMemory = useDynamicMemory 
            ? maxMemory.Filter(b => b != vmInfo.Value.MemoryMaximum)
            : None
        let command = PsCommandBuilder.Create()
            .AddCommand("Set-VMMemory")
            .AddParameter("VM", vmInfo.PsObject)
        let command2 = changedUseDynamicMemory.Match(
            Some: d => command.AddParameter("DynamicMemoryEnabled", d),
            None: () => command) 
        let command3 = changedStartupMemory.Match(
            Some: b => command2.AddParameter("StartupBytes", b),
            None: () => command2)
        let command4 = changedMinMemory.Match(
            Some: b => command3.AddParameter("MinimumBytes", b),
            None: () => command3)
        let command5 = changedMaxMemory.Match(
            Some: b => command4.AddParameter("MaximumBytes", b),
            None: () => command4)
        let message = "Updating catlet memory settings: "
            + string.Join("; ", Seq(
                    changedStartupMemory.Map(b => $"Startup memory {ToMiB(b)} MiB"),
                    changedUseDynamicMemory.Map(d => $"Dynamic memory {(d ? "enabled" : "disabled")}"),
                    changedMinMemory.Map(b => $"Minimum memory {ToMiB(b)} MiB"),
                    changedMaxMemory.Map(b => $"Maximum memory {ToMiB(b)} MiB"))
                .Somes())
            + "."
        from result in changedUseDynamicMemory.IsSome
                       || changedStartupMemory.IsSome
                       || changedMinMemory.IsSome
                       || changedMaxMemory.IsSome
            ? from _ in TryAsync(() => reportProgress(message).ToUnit()).ToEither()
              from __ in powershellEngine.RunAsync(command5).ToError()
              select unit
            : RightAsync<Error, Unit>(unit)
        from reloadedVmInfo in vmInfo.RecreateOrReload(powershellEngine)
        select reloadedVmInfo;

    private static Either<
            Error,
            (Option<long> Startup, Option<long> Minimum, Option<long> Maximum)>
        ValidateMemoryConfig(Option<CatletMemoryConfig> config) =>
        from _ in Right<Error, Unit>(unit)
        let startupMemory = config.Bind(c => Optional(c.Startup))
            .Filter(mb => mb > 0)
            .Map(ToBytes)
        let minimumMemory = config.Bind(c => Optional(c.Minimum))
            .Filter(mb => mb > 0)
            .Map(ToBytes)
        let maximumMemory = config.Bind(c => Optional(c.Maximum))
            .Filter(mb => mb > 0)
            .Map(ToBytes)
        let minimumError =
            from startup in startupMemory
            from min in minimumMemory
            where startup < min
            select Error.New($"Startup memory ({ToMiB(startup)} MiB) cannot be less than minimum memory ({ToMiB(min)} MiB).")
        from __ in minimumError.Match<Either<Error, Unit>>(Some: e => e, None: unit)
        let maximumError =
            from startup in startupMemory
            from max in maximumMemory
            where startup > max
            select Error.New($"Startup memory ({ToMiB(startup)} MiB) cannot be more than maximum memory ({ToMiB(max)} MiB).")
        from ___ in maximumError.Match<Either<Error, Unit>>(Some: e => e, None: unit)
        select (startupMemory, minimumMemory, maximumMemory);
    
    private static long ToBytes (int megaBytes) => megaBytes * 1024L * 1024;

    private static decimal ToMiB(long bytes) => bytes / (1024m * 1024L);
}
