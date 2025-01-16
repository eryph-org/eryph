using System;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging;

public class ConvergeSecureBoot(
    ConvergeContext context)
    : ConvergeTaskBase(context)
{
    public override Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        ConvergeSecureBootState(vmInfo).ToEither();
    
    private EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> ConvergeSecureBootState(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let expectedSecureBootState = CatletCapabilities.IsSecureBootEnabled(
            Context.Config.Capabilities.ToSeq())
        let expectedSecureBootTemplate = CatletCapabilities.FindSecureBootTemplate(
                Context.Config.Capabilities.ToSeq())
            .IfNone("MicrosoftWindows")
        from vmFirmwareInfo in GetFirmwareInfo(vmInfo)
        let currentSecureBootState = vmFirmwareInfo.SecureBoot == OnOffState.On
        let currentSecureBootTemplate = vmFirmwareInfo.SecureBootTemplate
        from __ in expectedSecureBootState == currentSecureBootState
                   && expectedSecureBootTemplate == currentSecureBootTemplate
            ? RightAsync<Error, Unit>(unit)
            : ConfigureSecureBoot(vmInfo, expectedSecureBootState, expectedSecureBootTemplate)
        from updatedVmInfo in vmInfo.RecreateOrReload(Context.Engine)
        select updatedVmInfo;

    private EitherAsync<Error, Unit> ConfigureSecureBoot(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        bool enableSecureBoot,
        string secureBootTemplate) =>
        // TODO check if VM is running?
        // if (vmInfo.Value.State == VirtualMachineState.Running)
        //     return Error.New("Cannot change secure boot settings of a running catlet.");
        from _1 in RightAsync<Error, Unit>(unit)
        let progressMessage = enableSecureBoot
            ? $"Configuring secure boot settings (Template: {secureBootTemplate})"
            : "Configuring secure boot settings (Secure Boot: Off)"
        from _2 in TryAsync(() => Context.ReportProgress(progressMessage).ToUnit()).ToEither()
        // Hyper-V allows us to set the SecureBootTemplate even if SecureBoot is disabled.
        // Hence, this works as expected.
        let command = PsCommandBuilder.Create()
            .AddCommand("Set-VMFirmware")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("EnableSecureBoot", enableSecureBoot ? OnOffState.On : OnOffState.Off)
            .AddParameter("SecureBootTemplate", secureBootTemplate)
        from _3 in Context.Engine.RunAsync(command).ToError().ToAsync()
        select unit;


    private EitherAsync<Error, VMFirmwareInfo> GetFirmwareInfo(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VMFirmware")
            .AddParameter("VM", vmInfo.PsObject)
        from vmSecurityInfos in Context.Engine.GetObjectValuesAsync<VMFirmwareInfo>(command)
            .ToError()
        from vMSecurityInfo in vmSecurityInfos.HeadOrNone()
            .ToEitherAsync(Error.New($"Failed to fetch firmware information for the VM {vmInfo.Value.Id}."))
        select vMSecurityInfo;
}
