﻿using System;
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
    public override Task<Either<Error, Unit>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        ConvergeSecureBootState(vmInfo).ToEither();
    
    private EitherAsync<Error, Unit> ConvergeSecureBootState(
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
        select unit;

    private EitherAsync<Error, Unit> ConfigureSecureBoot(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        bool enableSecureBoot,
        string secureBootTemplate) =>
        from _1 in guard(vmInfo.Value.State is VirtualMachineState.Off or VirtualMachineState.OffCritical,
                Error.New("Cannot change secure boot settings if the catlet is not stopped. Stop the catlet and retry."))
            .ToEitherAsync()
        from _2 in Context.ReportProgressAsync(enableSecureBoot
            ? $"Configuring secure boot settings (Template: {secureBootTemplate})"
            : "Configuring secure boot settings (Secure Boot: Off)")
        // Hyper-V allows us to set the SecureBootTemplate even if SecureBoot is disabled.
        // Hence, this works as expected.
        let command = PsCommandBuilder.Create()
            .AddCommand("Set-VMFirmware")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("EnableSecureBoot", enableSecureBoot ? OnOffState.On : OnOffState.Off)
            .AddParameter("SecureBootTemplate", secureBootTemplate)
        from _3 in Context.Engine.RunAsync(command)
        select unit;

    private EitherAsync<Error, VMFirmwareInfo> GetFirmwareInfo(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VMFirmware")
            .AddParameter("VM", vmInfo.PsObject)
        from optionalVmSecurityInfo in Context.Engine.GetObjectValueAsync<VMFirmwareInfo>(command)
        from vmSecurityInfo in optionalVmSecurityInfo.ToEitherAsync(Error.New(
            $"Failed to fetch firmware information for the VM {vmInfo.Value.Id}."))
        select vmSecurityInfo;
}
