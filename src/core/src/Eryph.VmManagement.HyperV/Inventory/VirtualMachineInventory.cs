using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.VmAgent;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using Eryph.VmManagement.Storage;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Inventory;

public class VirtualMachineInventory(
    IPowershellEngine engine,
    VmHostAgentConfiguration vmHostAgentConfig,
    IHostInfoProvider hostInfoProvider)
{
    public EitherAsync<Error, VirtualMachineData> InventorizeVM(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from hostInfo in hostInfoProvider.GetHostInfoAsync()
        from vmStorageSettings in VMStorageSettings.FromVm(vmHostAgentConfig, vmInfo)
        from diskStorageSettings in CurrentHardDiskDriveStorageSettings.Detect(
            engine, vmHostAgentConfig, vmInfo.GetList(x => x.HardDrives))
        from cpuData in GetCpuData(vmInfo)
        from memoryData in GetMemoryData(vmInfo)
        from firmwareData in GetFirmwareData(vmInfo)
        from securityData in GetSecurityData(vmInfo)
        from networks in VirtualNetworkQuery.GetNetworksByAdapters(hostInfo, vmInfo.GetList(x => x.NetworkAdapters))
        from networkAdapters in vmInfo.GetList(x => x.NetworkAdapters)
            .Map(a => a.CastSafe<VMNetworkAdapter>())
            .Sequence()
            .ToAsync()
        from networkAdaptersData in networkAdapters
            .Map(GetNetworkAdapterData)
            .SequenceSerial()
        select new VirtualMachineData
        {
            VmId = vmInfo.Value.Id,
            MetadataId = GetMetadataId(vmInfo),
            Status = VmStateUtils.toVmStatus(vmInfo.Value.State),
            Name = vmInfo.Value.Name,
            UpTime = vmInfo.Value.Uptime,
            Cpu = cpuData,
            Memory = memoryData,
            Firmware = firmwareData,
            Frozen = vmStorageSettings.Frozen,
            VMPath = vmStorageSettings.VMPath,
            StorageIdentifier = vmStorageSettings.StorageIdentifier.IfNone(""),
            ProjectId = vmStorageSettings.StorageNames.ProjectId.ToNullable(),
            ProjectName = vmStorageSettings.StorageNames.ProjectName.IfNone(""),
            DataStore = vmStorageSettings.StorageNames.DataStoreName.IfNone(""),
            Environment = vmStorageSettings.StorageNames.EnvironmentName.IfNone(""),
            Drives = CreateHardDriveInfo(diskStorageSettings, vmInfo.GetList(x => x.HardDrives)).ToArray(),
            NetworkAdapters = networkAdaptersData.ToArray(),
            Networks = networks.ToArray(),
            Security = securityData,
        };

    private EitherAsync<Error, VirtualMachineNetworkAdapterData> GetNetworkAdapterData(
        TypedPsObject<VMNetworkAdapter> adapter) =>
        from macAddress in Optional(adapter.Value.MacAddress)
            // Hyper-V returns all zeros when a dynamic MAC address has not been assigned yet.
            .Filter(a => a != "000000000000")
            .Map(EryphMacAddress.NewEither)
            .Sequence()
            .ToAsync()
        select new VirtualMachineNetworkAdapterData
        {
            Id = adapter.Value.Id,
            AdapterName = adapter.Value.Name,
            VirtualSwitchName = adapter.Value.SwitchName,
            VirtualSwitchId = adapter.Value.SwitchId,
            MacAddress = macAddress.Map(a => a.Value).IfNoneUnsafe((string)null),
        };

    private EitherAsync<Error, VirtualMachineCpuData> GetCpuData(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddInput(vmInfo.PsObject)
            .AddCommand("Get-VMProcessor")
        from optionalCpuData in engine.GetObjectValueAsync<VirtualMachineCpuData>(command)
        from cpuData in optionalCpuData.ToEitherAsync(
            Error.New($"Could not fetch processor information of VM {vmInfo.Value.Id}."))
        select cpuData;

    private EitherAsync<Error, VirtualMachineMemoryData> GetMemoryData(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddInput(vmInfo.PsObject)
            .AddCommand("Get-VMMemory")
        from optionalMemoryData in engine.GetObjectValueAsync<VirtualMachineMemoryData>(command)
        from memoryData in optionalMemoryData.ToEitherAsync(
            Error.New($"Could not fetch memory information of VM {vmInfo.Value.Id}."))
        select memoryData;

    private EitherAsync<Error, VirtualMachineFirmwareData> GetFirmwareData(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from firmwareData in vmInfo.Value.Generation == 1
            ? RightAsync<Error, VirtualMachineFirmwareData>(new VirtualMachineFirmwareData())
            : GetGen2FirmwareData(vmInfo)
        select firmwareData;

    private EitherAsync<Error, VirtualMachineFirmwareData> GetGen2FirmwareData(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddInput(vmInfo.PsObject)
            .AddCommand("Get-VMFirmware")
        from optionalFirmwareInfo in engine.GetObjectValueAsync<VMFirmwareInfo>(command)
        from firmwareInfo in optionalFirmwareInfo.ToEitherAsync(
            Error.New($"Could not fetch firmware information of VM {vmInfo.Value.Id}."))
        select new VirtualMachineFirmwareData
        {
            SecureBoot = firmwareInfo.SecureBoot == OnOffState.On,
            SecureBootTemplate = firmwareInfo.SecureBootTemplate
        };

    private EitherAsync<Error, VirtualMachineSecurityData> GetSecurityData(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from _ in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VMSecurity")
            .AddParameter("VM", vmInfo.PsObject)
        from optionalVmSecurityInfo in engine.GetObjectValueAsync<VMSecurityInfo>(command)
        from vMSecurityInfo in optionalVmSecurityInfo.ToEitherAsync(Error.New(
            $"Failed to fetch security information for the VM {vmInfo.Value.Id}."))
        select new VirtualMachineSecurityData
        {
            BindToHostTpm = vMSecurityInfo.BindToHostTpm,
            EncryptStateAndVmMigrationTraffic = vMSecurityInfo.EncryptStateAndVmMigrationTraffic,
            KsdEnabled = vMSecurityInfo.KsdEnabled,
            Shielded = vMSecurityInfo.Shielded,
            TpmEnabled = vMSecurityInfo.TpmEnabled,
            VirtualizationBasedSecurityOptOut = vMSecurityInfo.VirtualizationBasedSecurityOptOut,
        };

    private static Guid GetMetadataId(TypedPsObject<VirtualMachineInfo> vmInfo)
    {
        var notes = vmInfo.Value.Notes;

        var metadataIdString = "";
        if (!string.IsNullOrWhiteSpace(notes))
        {
            var metadataIndex = notes.IndexOf("Eryph metadata id: ", StringComparison.OrdinalIgnoreCase);
            if (metadataIndex != -1)
            {
                var metadataEnd = metadataIndex + "Eryph metadata id: ".Length + 36;
                if (metadataEnd <= notes.Length)
                    metadataIdString = notes.Substring(metadataIndex + "Eryph metadata id: ".Length, 36);
            }
        }

        return !Guid.TryParse(metadataIdString, out var metadataId)
            ? Guid.Empty
            : metadataId;
    }

    private static IEnumerable<VirtualMachineDriveData> CreateHardDriveInfo(
        Seq<CurrentHardDiskDriveStorageSettings> storageSettings,
        IEnumerable<TypedPsObject<VirtualMachineDeviceInfo>> hdDevices)
    {
        foreach (var device in hdDevices)
        {
            var hd = device.Cast<HardDiskDriveInfo>().Value;
            var storageSetting = storageSettings.FirstOrDefault(x => x.AttachedVMId == hd.Id);

            var drive = new VirtualMachineDriveData
            {
                Id = hd.Id,
                ControllerLocation = hd.ControllerLocation,
                ControllerNumber = hd.ControllerNumber,
                ControllerType = hd.ControllerType,
                Frozen = storageSetting?.Frozen ?? true,
                Type = storageSetting?.Type
            };

            if (storageSetting?.DiskSettings != null)
                drive.Disk = storageSetting.DiskSettings.CreateDiskInfo();

            yield return drive;
        }
    }
}
