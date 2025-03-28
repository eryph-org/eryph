using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
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
        from _ in RightAsync<Error, Unit>(unit)
        let memoryStartupBytes = startupMemory.GetValueOrDefault(EryphConstants.DefaultCatletMemoryMb) * 1024L * 1024
        let createVmCommand = PsCommandBuilder.Create()
            .AddCommand("New-VM")
            .AddParameter("Name", storageIdentifier)
            .AddParameter("Path", vmPath)
            .AddParameter("MemoryStartupBytes", memoryStartupBytes)
            .AddParameter("Generation", 2)
        from optionalVmInfo in engine.GetObjectAsync<VirtualMachineInfo>(createVmCommand)
        from created in optionalVmInfo.ToEitherAsync(Error.New("Failed to create VM"))
        let removeNetworkAdaptersCommand = PsCommandBuilder.Create()
            .AddCommand("Get-VMNetworkAdapter")
            .AddParameter("VM", created.PsObject)
            .AddCommand("Remove-VMNetworkAdapter")
        from _2 in engine.RunAsync(removeNetworkAdaptersCommand)
        from renamed in Rename(engine, created, vmName)
        from result in SetDefaults(engine, renamed)
        select result;

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
        CatletMetadata metadata,
        MachineNetworkSettings[] networkSetting,
        VMStorageSettings storageSettings,
        Seq<UniqueGeneIdentifier> resolvedGenes) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let convergeContext = new ConvergeContext(
            vmHostAgentConfig, engine, portManager, reportProgress, machineConfig, 
            metadata, storageSettings, networkSetting, hostInfo, resolvedGenes)
        let convergeTasks = Seq<ConvergeTaskBase>(
            new ConvergeSecureBoot(convergeContext),
            new ConvergeTpm(convergeContext),
            new ConvergeCPU(convergeContext),
            new ConvergeNestedVirtualization(convergeContext),
            new ConvergeMemory(convergeContext),
            new ConvergeDrives(convergeContext),
            new ConvergeNetworkAdapters(convergeContext))
        let vmId = vmInfo.Value.Id
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
        CatletMetadata metadata,
        VMStorageSettings storageSettings) =>
        from _1 in RightAsync<Error, Unit>(unit)
        // Pass empty MachineNetworkSettings as converging the cloud init disk
        // does not require them.
        let convergeContext = new ConvergeContext(
            vmHostAgentConfig, engine, portManager, reportProgress, machineConfig,
            metadata, storageSettings, [], hostInfo, Empty)
        let convergeTasks = Seq1<ConvergeTaskBase>(
            new ConvergeCloudInitDisk(convergeContext))
        let vmId = vmInfo.Value.Id
        from _2 in convergeTasks
            .Map(task => from reloadedVmInfo in VmQueries.GetVmInfo(engine, vmId)
                from _ in task.Converge(reloadedVmInfo).ToAsync()
                select unit)
            .SequenceSerial()
        from reloadedVmInfo in VmQueries.GetVmInfo(engine, vmId)
        select reloadedVmInfo;
}
