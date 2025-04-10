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

public class ConvergeNestedVirtualization(
    ConvergeContext context)
    : ConvergeTaskBase(context)
{
    public override Task<Either<Error, Unit>> Converge(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        ConvergeNestedVirtualizationState(vmInfo).ToEither();

    private EitherAsync<Error, Unit> ConvergeNestedVirtualizationState(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let expectedNestedVirtualization = CatletCapabilities.IsNestedVirtualizationEnabled(
            Context.Config.Capabilities.ToSeq())
        from vmProcessorInfo in GetVmProcessorInfo(vmInfo)
        let actualNestedVirtualization = vmProcessorInfo.ExposeVirtualizationExtensions
        from __ in expectedNestedVirtualization == actualNestedVirtualization
            ? RightAsync<Error, Unit>(unit)
            : ConfigureNestedVirtualization(vmInfo, expectedNestedVirtualization)
        select unit;

    private EitherAsync<Error, Unit> ConfigureNestedVirtualization(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        bool exposeVirtualizationExtensions) =>
        from _1 in guard(vmInfo.Value.State is VirtualMachineState.Off or VirtualMachineState.OffCritical,
                Error.New("Cannot change virtualization settings if the catlet is not stopped. Stop the catlet and retry."))
            .ToEitherAsync()
        from _2 in Context.ReportProgressAsync(exposeVirtualizationExtensions
            ? "Enabling nested virtualization."
            : "Disabling nested virtualization.")
        let command = PsCommandBuilder.Create()
            .AddCommand("Set-VMProcessor")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("ExposeVirtualizationExtensions", exposeVirtualizationExtensions)
        from _3 in Context.Engine.RunAsync(command)
        select unit;

    private EitherAsync<Error, VMProcessorInfo> GetVmProcessorInfo(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VMProcessor")
            .AddParameter("VM", vmInfo.PsObject)
        from optionalVmSecurityInfo in Context.Engine.GetObjectValueAsync<VMProcessorInfo>(command)
        from vmSecurityInfo in optionalVmSecurityInfo.ToEitherAsync(Error.New(
            $"Failed to fetch processor information for the VM {vmInfo.Value.Id}."))
        select vmSecurityInfo;
}
