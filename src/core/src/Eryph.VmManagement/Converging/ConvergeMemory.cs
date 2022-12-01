using System.Threading.Tasks;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Converging;

public class ConvergeMemory : ConvergeTaskBase
{
    public ConvergeMemory(ConvergeContext context) : base(context)
    {
    }

    public override async Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo)
    {
        var dynamicMemoryOn = Context.Config.VM.Memory.Maximum.HasValue || Context.Config.VM.Memory.Minimum.HasValue;

        if (vmInfo.Value.DynamicMemoryEnabled != dynamicMemoryOn)
        {
            var onOffString = dynamicMemoryOn ? "Enabling" : "Disabling";
            await Context.ReportProgress($"{onOffString} dynamic memory").ConfigureAwait(false);

            await Context.Engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Set-VMMemory")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("DynamicMemoryEnabled", dynamicMemoryOn)).ConfigureAwait(false);

            if (dynamicMemoryOn)
            {
                if (Context.Config.VM.Memory.Maximum.HasValue)
                {
                    var maxMemoryBytes = Context.Config.VM.Memory.Maximum.GetValueOrDefault() * 1024L * 1024;
                    if (vmInfo.Value.MemoryMaximum != maxMemoryBytes)
                    {
                        await Context
                            .ReportProgress(
                                $"Setting maximum memory to {Context.Config.VM.Memory.Maximum.GetValueOrDefault()} MB")
                            .ConfigureAwait(false);

                        await Context.Engine.RunAsync(PsCommandBuilder.Create()
                            .AddCommand("Set-VMMemory")
                            .AddParameter("VM", vmInfo.PsObject)
                            .AddParameter("MaximumBytes", maxMemoryBytes)).ConfigureAwait(false);
                    }
                }

                if (Context.Config.VM.Memory.Minimum.HasValue)
                {
                    var minMemoryBytes = Context.Config.VM.Memory.Minimum.GetValueOrDefault(Context.Config.VM.Memory.Startup.GetValueOrDefault()) * 1024L * 1024;

                    if (vmInfo.Value.MemoryMinimum != minMemoryBytes)
                    {
                        await Context
                            .ReportProgress(
                                $"Setting minimum memory to {Context.Config.VM.Memory.Minimum.GetValueOrDefault()} MB")
                            .ConfigureAwait(false);

                        await Context.Engine.RunAsync(PsCommandBuilder.Create()
                            .AddCommand("Set-VMMemory")
                            .AddParameter("VM", vmInfo.PsObject)
                            .AddParameter("MinimumBytes", minMemoryBytes)).ConfigureAwait(false);
                    }
                }
            }
        }

        var memoryStartupBytes = Context.Config.VM.Memory.Startup.GetValueOrDefault(1024) * 1024L * 1024;
        if (memoryStartupBytes != vmInfo.Value.MemoryStartup)
        {
            await Context.ReportProgress($"Setting startup memory to {Context.Config.VM.Memory.Startup.GetValueOrDefault(1024)} MB").ConfigureAwait(false);

            await Context.Engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Set-VMMemory")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("StartupBytes", memoryStartupBytes)).ConfigureAwait(false);
        }


        return await vmInfo.RecreateOrReload(Context.Engine).ToEither().ConfigureAwait(false);
    }
}