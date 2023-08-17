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
        var dynamicMemoryOn = (Context.Config.Memory?.Maximum).GetValueOrDefault() != 0 || (Context.Config.Memory?.Minimum).GetValueOrDefault() != 0;

        if (vmInfo.Value.DynamicMemoryEnabled != dynamicMemoryOn)
        {
            var onOffString = dynamicMemoryOn ? "Enabling" : "Disabling";
            await Context.ReportProgress($"{onOffString} dynamic memory").ConfigureAwait(false);

            var result = await Context.Engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Set-VMMemory")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("DynamicMemoryEnabled", dynamicMemoryOn)).ConfigureAwait(false);

            if (result.IsLeft)
                return result.Map(_ => vmInfo).ToError();

            if (dynamicMemoryOn)
            {
                if ((Context.Config.Memory?.Maximum).HasValue)
                {
                    var maxMemoryBytes = Context.Config.Memory?.Maximum.GetValueOrDefault() * 1024L * 1024;
                    if (vmInfo.Value.MemoryMaximum != maxMemoryBytes)
                    {
                        await Context
                            .ReportProgress(
                                $"Setting maximum memory to {Context.Config.Memory?.Maximum.GetValueOrDefault()} MB")
                            .ConfigureAwait(false);

                        result = await Context.Engine.RunAsync(PsCommandBuilder.Create()
                            .AddCommand("Set-VMMemory")
                            .AddParameter("VM", vmInfo.PsObject)
                            .AddParameter("MaximumBytes", maxMemoryBytes)).ConfigureAwait(false);

                        if (result.IsLeft)
                            return result.Map(_ => vmInfo).ToError();

                    }
                }

                if ((Context.Config.Memory?.Minimum).HasValue)
                {
                    var minMemoryBytes = Context.Config.Memory?.Minimum.GetValueOrDefault(Context.Config.Memory.Startup.GetValueOrDefault()) * 1024L * 1024;

                    if (vmInfo.Value.MemoryMinimum != minMemoryBytes)
                    {
                        await Context
                            .ReportProgress(
                                $"Setting minimum memory to {Context.Config.Memory?.Minimum.GetValueOrDefault()} MB")
                            .ConfigureAwait(false);

                        result = await Context.Engine.RunAsync(PsCommandBuilder.Create()
                            .AddCommand("Set-VMMemory")
                            .AddParameter("VM", vmInfo.PsObject)
                            .AddParameter("MinimumBytes", minMemoryBytes)).ConfigureAwait(false);

                        if (result.IsLeft)
                            return result.Map(_ => vmInfo).ToError();

                    }
                }
            }
        }

        var memoryStartupBytes = Context.Config.Memory?.Startup.GetValueOrDefault(1024) * 1024L * 1024;
        if (memoryStartupBytes != vmInfo.Value.MemoryStartup)
        {
            await Context.ReportProgress($"Setting startup memory to {Context.Config.Memory?.Startup.GetValueOrDefault(1024)} MB").ConfigureAwait(false);
                
            var result = await Context.Engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Set-VMMemory")
                .AddParameter("VM", vmInfo.PsObject)
                .AddParameter("StartupBytes", memoryStartupBytes)).ConfigureAwait(false);

            if (result.IsLeft)
                return result.Map(_ => vmInfo).ToError();

        }


        return await vmInfo.RecreateOrReload(Context.Engine).ToEither().ConfigureAwait(false);
    }
}