using System;
using System.Linq;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Converging;

public class ConvergeSecureBoot : ConvergeTaskBase
{
    public ConvergeSecureBoot(ConvergeContext context) : base(context)
    {
    }

    public override async Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo)
    {
        var secureBootCapability = Context.Config.Capabilities?.FirstOrDefault(x => x.Name ==
            EryphConstants.Capabilities.SecureBoot);

        if (secureBootCapability == null)
            return vmInfo;

        var templateName =
            secureBootCapability.Details?.FirstOrDefault(x => x.StartsWith("template:", 
                StringComparison.OrdinalIgnoreCase))?.Split(':')[1]
            ?? "MicrosoftWindows";

        var onOffState = (secureBootCapability.Details?.Any(x =>
            string.Equals(x, EryphConstants.CapabilityDetails.Disabled, StringComparison.OrdinalIgnoreCase))).GetValueOrDefault() ? OnOffState.Off : OnOffState.On;

        return await (from currentFirmware in Context.Engine.GetObjectsAsync<VMFirmwareInfo>(new PsCommandBuilder()
                .AddCommand("get-VMFirmware")
                .AddArgument(vmInfo.PsObject)).ToError().ToAsync().Bind(
                r => r.HeadOrLeft(Error.New("VM firmware not found")).ToAsync())
            from uSecureBoot in currentFirmware.Value.SecureBootTemplate == templateName && currentFirmware.Value.SecureBoot == onOffState
                ? Unit.Default
                : Unit.Default.Apply(async _ =>
                {
                    if (vmInfo.Value.State == VirtualMachineState.Running)
                        return Error.New("Cannot change secure boot settings of a running catlet.");

                    if(onOffState == OnOffState.On)
                        await Context.ReportProgress($"Configuring secure boot settings (Template: {templateName})").ConfigureAwait(false);
                    else
                        await Context.ReportProgress($"Configuring secure boot settings (Secure Boot: Off)").ConfigureAwait(false);

                    return await Context.Engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Set-VMFirmware")
                        .AddParameter("VM", vmInfo.PsObject)
                        .AddParameter("EnableSecureBoot", onOffState)
                        .AddParameter("SecureBootTemplate", templateName)).ToError();

                }).ToAsync()
            from newVmInfo in vmInfo.RecreateOrReload(Context.Engine)
            select newVmInfo).ToEither();


    }
}