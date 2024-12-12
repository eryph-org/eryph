using System;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Converging;

public class ConvergeTpm(ConvergeContext context) : ConvergeTaskBase(context)
{
    public override Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        ConvergeTpmState(vmInfo).ToEither();
    
    private EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> ConvergeTpmState(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let tpmCapability = Context.Config.Capabilities.ToSeq()
            .Find(c => c.Name == EryphConstants.Capabilities.Tpm)
        let expectedTpmState = tpmCapability.Map(IsEnabled).IfNone(false)
        from vmSecurityInfo in GetVmSecurityInfo(vmInfo)
        let currentTpmState = vmSecurityInfo.TpmEnabled
        from __ in expectedTpmState == currentTpmState
            ? RightAsync<Error, Unit>(unit)
            : ConfigureTpm(vmInfo, expectedTpmState)
        from updatedVmInfo in vmInfo.RecreateOrReload(Context.Engine)
        select updatedVmInfo;
    
    private EitherAsync<Error, VMSecurityInfo> GetVmSecurityInfo(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VMSecurity")
            .AddParameter("VM", vmInfo.PsObject)
        from vmSecurityInfos in Context.Engine.GetObjectValuesAsync<VMSecurityInfo>(command)
            .ToError()
        from vMSecurityInfo in vmSecurityInfos.HeadOrNone()
            .ToEitherAsync(Error.New($"Failed to fetch security information for the VM {vmInfo.Value.Id}."))
        select vMSecurityInfo;
    
    private EitherAsync<Error, Unit> ConfigureTpm(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        bool enableTpm) =>
        enableTpm ? EnableTpm(vmInfo) : DisableTpm(vmInfo);
    
    private EitherAsync<Error, Unit> EnableTpm(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in EnsureKeyProtector(vmInfo)
        let command = PsCommandBuilder.Create()
            .AddCommand("Enable-VMTPM")
            .AddParameter("VM", vmInfo.PsObject)
        from __ in Context.Engine.RunAsync(command).ToError().ToAsync()
        select unit;

    private EitherAsync<Error, Unit> EnsureKeyProtector(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        // Get-VMKeyProtector returns the protector as a byte array. Even when no protector exist, 
        // Hyper-V returns a short byte array (e.g. [0, 0, 0, 4]). We check for minimum length of 16
        // to decide whether a protector exists.
        let getCommand = PsCommandBuilder.Create()
            .AddCommand("Get-VMKeyProtector")
            .AddParameter("VM", vmInfo.PsObject)
            .Script("Where-Object { $_.Length -ge 16 } | ForEach-Object { ConvertTo-HgsKeyProtector -Bytes $_ }")
        from keyProtectors in Context.Engine.GetObjectValuesAsync<CimHgsKeyProtector>(getCommand)
            .ToError()
        let hasKeyProtector = keyProtectors.HeadOrNone()
        from __ in hasKeyProtector
            ? RightAsync<Error, Unit>(unit)
            : CreateKeyProtector(vmInfo)
        select unit;

    private EitherAsync<Error, TypedPsObject<CimHgsGuardian>> EnsureHgsGuardian() =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-HgsGuardian")
            .AddParameter("Name", EryphConstants.HgsGuardianName)
        from existingGuardians in Context.Engine.GetObjectsAsync<CimHgsGuardian>(command)
            .ToError().ToAsync()
        from guardian in existingGuardians.HeadOrNone().Match(
                Some: g => g,
                None: CreateHgsGuardian)
        select guardian;

    private EitherAsync<Error, TypedPsObject<CimHgsGuardian>> CreateHgsGuardian() =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("New-HgsGuardian")
            .AddParameter("Name", EryphConstants.HgsGuardianName)
            .AddParameter("GenerateCertificates")
        from results in Context.Engine.GetObjectsAsync<CimHgsGuardian>(command)
            .ToError().ToAsync()
        from guardian in results.HeadOrNone()
            .ToEitherAsync(Error.New("Failed to create HGS guardian."))
        select guardian;

    private EitherAsync<Error, Unit> CreateKeyProtector(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from guardian in EnsureHgsGuardian()
        let createCommand = PsCommandBuilder.Create()
            .AddCommand("New-HgsKeyProtector")
            .AddParameter("Owner", guardian.PsObject)
            .AddParameter("AllowUntrustedRoot")
            .AddParameter("AllowExpired")
        from protectors in Context.Engine.GetObjectsAsync<CimHgsKeyProtector>(createCommand)
            .ToError().ToAsync()
        from protector in protectors.HeadOrNone()
            .ToEitherAsync(Error.New("Failed to create HGS key protector."))
        let command = PsCommandBuilder.Create()
            .AddCommand("Set-VMKeyProtector")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("KeyProtector", protector.Value.RawData)
        from _ in Context.Engine.RunAsync(command).ToError().ToAsync()
        select unit;

    private EitherAsync<Error, Unit> DisableTpm(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Disable-VMTPM")
            .AddParameter("VM", vmInfo.PsObject)
        from __ in Context.Engine.RunAsync(command).ToError().ToAsync()
        select unit;
    
    private bool IsEnabled(CatletCapabilityConfig capabilityConfig) =>
        capabilityConfig.Details.ToSeq()
            .All(d => !string.Equals(d, EryphConstants.CapabilityDetails.Disabled, StringComparison.OrdinalIgnoreCase));
}
