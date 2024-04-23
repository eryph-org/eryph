using System;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Converging;

public class ConvergeNestedVirtualization : ConvergeTaskBase
{
    public ConvergeNestedVirtualization(ConvergeContext context) : base(context)
    {
    }

    public override async Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo)
    {
        var capability = Context.Config.Capabilities?.FirstOrDefault(x => x.Name ==
            EryphConstants.Capabilities.NestedVirtualization);

        if (capability == null)
            return vmInfo;

        var onOffState = (capability.Details?.Any(x =>
            string.Equals(x, "off", StringComparison.InvariantCultureIgnoreCase))).GetValueOrDefault() ? OnOffState.Off : OnOffState.On;

        return await (from exposedExtensions in Context.Engine.GetObjectValuesAsync<bool>(new PsCommandBuilder()
                .AddCommand("Get-VMProcessor")
                .AddParameter("VM",vmInfo.PsObject)
                .AddCommand("Select-Object")
                .AddParameter("ExpandProperty", "ExposeVirtualizationExtensions")
            ).ToError().Bind(
                r => r.HeadOrLeft(Error.New("Failed to read processor details.")).ToAsync())
            let currentOnOffState = exposedExtensions ? OnOffState.On : OnOffState.Off
            from uNestedVirtualization in currentOnOffState == onOffState
                ? Unit.Default
                : Unit.Default.Apply(async _ =>
                {
                    if (vmInfo.Value.State == VirtualMachineState.Running)
                        return Error.New("Cannot change nested virtualization settings of a running catlet.");

                    if(onOffState == OnOffState.On)
                        await Context.ReportProgress("Enabling nested virtualization.").ConfigureAwait(false);
                    else
                        await Context.ReportProgress("Disabling nested virtualization.").ConfigureAwait(false);

                    return await Context.Engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Set-VMProcessor")
                        .AddParameter("VM", vmInfo.PsObject)
                        .AddParameter("ExposeVirtualizationExtensions", onOffState == OnOffState.On)
                        ).ToError();
                }).ToAsync()
            from newVmInfo in vmInfo.RecreateOrReload(Context.Engine)
            select newVmInfo).ToEither();


    }
}