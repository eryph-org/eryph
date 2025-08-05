using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN.Windows;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.GenePool.Model;
using Eryph.Resources.Disks;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Data.Planned;
using Eryph.VmManagement.Inventory;
using Eryph.VmManagement.Storage;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;


namespace Eryph.VmManagement;

public static class VirtualMachine
{
    public static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> Create(
        IPowershellEngine engine,
        string vmName,
        string storageIdentifier,
        string vmPath,
        int? startupMemory) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let memoryStartupBytes = startupMemory.GetValueOrDefault(EryphConstants.DefaultCatletMemoryMb) * 1024L * 1024
        let createVmCommand = PsCommandBuilder.Create()
            .AddCommand("New-VM")
            .AddParameter("Name", storageIdentifier)
            .AddParameter("Path", vmPath)
            .AddParameter("MemoryStartupBytes", memoryStartupBytes)
            .AddParameter("Generation", 2)
        from optionalVmInfo in engine.GetObjectAsync<VirtualMachineInfo>(createVmCommand)
        from created in optionalVmInfo.ToEitherAsync(Error.New("Failed to create VM"))
        from renamed in Rename(engine, created, vmName)
        let removeNetworkAdaptersCommand = PsCommandBuilder.Create()
            .AddCommand("Get-VMNetworkAdapter")
            .AddParameter("VM", renamed.PsObject)
            .AddCommand("Remove-VMNetworkAdapter")
        from _2 in engine.RunAsync(removeNetworkAdaptersCommand)
        from _3 in EnsureDefaultNetworkAdapterWasRemoved(engine, created.Value.Id)
        from adapterRemoved in VmQueries.GetVmInfo(engine, created.Value.Id)
        from result in SetDefaults(engine, adapterRemoved)
        select result;

    /// <summary>
    /// This method verifies that the VM with <paramref name="vmId"/> has no
    /// network adapters.
    /// </summary>
    /// <remarks>
    /// There is a rare situation where <c>Get-VM</c> reports a network adapter
    /// for a VM even after all adapters have been removed with <c>Remove-VMNetworkAdapter</c>.
    /// This method retries the check whether all network adapters have been removed until
    /// it is successful.
    /// </remarks>
    private static EitherAsync<Error, Unit> EnsureDefaultNetworkAdapterWasRemoved(
        IPowershellEngine engine,
        Guid vmId) =>
        TryAsync(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();
                var result = await VmQueries.GetVmInfo(engine, vmId, cts.Token);
                result.IfLeft(e => e.Throw());
                var vmInfo = result.ValueUnsafe();
                if (vmInfo.Value.NetworkAdapters.Length == 0)
                    return unit;

                await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
            }
        }).ToEither(e => Error.New($"Failed to remove default network adapter after creating VM '{vmId}'.", e));

    public static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> Rename(
        IPowershellEngine engine,
        TypedPsObject<VirtualMachineInfo> vmInfo,
        string newName) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Rename-VM")
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("NewName", newName)
        from _2 in engine.RunAsync(command)
        from reloadedVmInfo in vmInfo.Reload(engine)
        select reloadedVmInfo;

    public static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> SetDefaults(
        IPowershellEngine engine,
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from optionalSetVmCommand in engine.GetObjectAsync<PowershellCommand>(
                PsCommandBuilder.Create().AddCommand("Get-Command").AddArgument("Set-VM"))
        from setVmCommand in optionalSetVmCommand.ToEitherAsync(
            Error.New("The Powershell command Set-VM was not found."))
        let builder = BuildSetVMCommand(vmInfo, setVmCommand)
        from uSet in engine.RunAsync(builder)
        from reloaded in vmInfo.Reload(engine)
        select reloaded;

    private static PsCommandBuilder BuildSetVMCommand(TypedPsObject<VirtualMachineInfo> vmInfo, PowershellCommand commandInfo)
    {
        var builder = new PsCommandBuilder().AddCommand("Set-VM");
        builder
            .AddParameter("VM", vmInfo.PsObject)
            .AddParameter("DynamicMemory", false)
            .AddParameter("AutomaticStartAction", "Nothing")
            .AddParameter("AutomaticStopAction", "Save");

        if (commandInfo.Parameters.ContainsKey("AutomaticCheckpointsEnabled"))
            builder.AddParameter("AutomaticCheckpointsEnabled", false);

        if (commandInfo.Parameters.ContainsKey("EnhancedSessionTransportType"))
            builder.AddParameter("EnhancedSessionTransportType", "VMBus");

        return builder;
    }

    public static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> Converge(
        VmHostAgentConfiguration vmHostAgentConfig,
        VMHostMachineData hostInfo,
        IPowershellEngine engine,
        IHyperVOvsPortManager portManager,
        Func<string, Task> reportProgress,
        TypedPsObject<VirtualMachineInfo> vmInfo,
        CatletConfig machineConfig,
        Guid catletId,
        MachineNetworkSettings[] networkSetting,
        VMStorageSettings storageSettings,
        Seq<UniqueGeneIdentifier> resolvedGenes,
        ILoggerFactory loggerFactory) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let vmId = vmInfo.Value.Id
        // Pass false for SecretDataHidden as we do not touch the config drive
        let convergeContext = new ConvergeContext(
            vmHostAgentConfig, engine, portManager, reportProgress, machineConfig, 
            catletId, vmId, false, storageSettings, networkSetting, hostInfo, resolvedGenes,
            loggerFactory)
        let convergeTasks = Seq<ConvergeTaskBase>(
            new ConvergeSecureBoot(convergeContext),
            new ConvergeTpm(convergeContext),
            new ConvergeCPU(convergeContext),
            new ConvergeNestedVirtualization(convergeContext),
            new ConvergeMemory(convergeContext),
            new ConvergeDrives(convergeContext),
            new ConvergeNetworkAdapters(convergeContext))
        from _2 in convergeTasks
            .Map(task => from reloadedVmInfo in VmQueries.GetVmInfo(engine, vmId)
                         from _ in task.Converge(reloadedVmInfo).ToAsync()
                         select unit)
            .SequenceSerial()
        from reloadedVmInfo in VmQueries.GetVmInfo(engine, vmId)
        select reloadedVmInfo;

    public static EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> ConvergeConfigDrive(
        VmHostAgentConfiguration vmHostAgentConfig,
        VMHostMachineData hostInfo,
        IPowershellEngine engine,
        IHyperVOvsPortManager portManager,
        Func<string, Task> reportProgress,
        TypedPsObject<VirtualMachineInfo> vmInfo,
        CatletConfig machineConfig,
        Guid catletId,
        bool secretDataHidden,
        VMStorageSettings storageSettings,
        ILoggerFactory loggerFactory) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let vmId = vmInfo.Value.Id
        // Pass empty MachineNetworkSettings as converging the cloud init disk
        // does not require them.
        let convergeContext = new ConvergeContext(
            vmHostAgentConfig, engine, portManager, reportProgress, machineConfig,
            catletId, vmId, secretDataHidden, storageSettings, [], hostInfo, Empty, loggerFactory)
        let convergeTasks = Seq1<ConvergeTaskBase>(
            new ConvergeCloudInitDisk(convergeContext))
        from _2 in convergeTasks
            .Map(task => from reloadedVmInfo in VmQueries.GetVmInfo(engine, vmId)
                from _ in task.Converge(reloadedVmInfo).ToAsync()
                select unit)
            .SequenceSerial()
        from reloadedVmInfo in VmQueries.GetVmInfo(engine, vmId)
        select reloadedVmInfo;
}
