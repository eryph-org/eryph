using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Core.VmAgent;
using Eryph.Resources.Disks;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using Eryph.VmManagement.Storage;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Inventory
{
    public class VirtualMachineInventory
    {
        private readonly IPowershellEngine _engine;
        private readonly VmHostAgentConfiguration _vmHostAgentConfig;
        private readonly IHostInfoProvider _hostInfoProvider;

        public VirtualMachineInventory(IPowershellEngine engine, VmHostAgentConfiguration vmHostAgentConfig, IHostInfoProvider hostInfoProvider)
        {
            _engine = engine;
            _vmHostAgentConfig = vmHostAgentConfig;
            _hostInfoProvider = hostInfoProvider;
        }

        public Task<Either<Error,VirtualMachineData>> InventorizeVM(
            TypedPsObject<VirtualMachineInfo> vmInfo)

        {
           return (from hostInfo in _hostInfoProvider.GetHostInfoAsync()
               from vm in Prelude.RightAsync<Error, TypedPsObject<VirtualMachineInfo>>(vmInfo)
                    
               from vmStorageSettings in VMStorageSettings.FromVM(_vmHostAgentConfig, vm)
               from diskStorageSettings in CurrentHardDiskDriveStorageSettings.Detect(
                   _engine, _vmHostAgentConfig, vm.GetList(x=>x.HardDrives))
               from cpuData in GetCpuData(vmInfo)
               from memoryData in GetMemoryData(vmInfo)
               from firmwareData in GetFirmwareData(vmInfo)
               select new VirtualMachineData
                {
                    VMId = vm.Value.Id,
                    MetadataId = GetMetadataId(vm),
                    Status = InventoryConverter.MapVmInfoStatusToVmStatus(vm.Value.State),
                    Name = vm.Value.Name,
                    UpTime = vm.Value.Uptime,
                    Cpu = cpuData,
                    Memory = memoryData,
                    Firmware = firmwareData,
                    Frozen = vmStorageSettings.Map(x => x.Frozen).IfNone(true),
                    VMPath = vmStorageSettings.Map(x => x.VMPath).IfNone(""),
                    StorageIdentifier = vmStorageSettings.Map(x=>x.StorageIdentifier.IfNone("")).IfNone(""),
                    ProjectName = vmStorageSettings.Map(x => x.StorageNames.ProjectName.IfNone("")).IfNone(""),
                    DataStore = vmStorageSettings.Map(x => x.StorageNames.DataStoreName.IfNone("")).IfNone(""),
                    Environment = vmStorageSettings.Map(x => x.StorageNames.EnvironmentName.IfNone("")).IfNone(""),
                    Drives = CreateHardDriveInfo(diskStorageSettings, vmInfo.GetList(x=>x.HardDrives)).ToArray(),
                    NetworkAdapters = vm.GetList(x=>x.NetworkAdapters).Map(a=>
                    {
                        var connectedAdapter = a.Cast<VMNetworkAdapter>();
                        var res = new VirtualMachineNetworkAdapterData
                        {
                            Id = a.Value.Id,
                            AdapterName = a.Value.Name,
                            VirtualSwitchName = connectedAdapter.Value.SwitchName,
                            VirtualSwitchId = connectedAdapter.Value.SwitchId,
                        };
                        return res;
                    }).ToArray(),
                    Networks = VirtualNetworkQuery.GetNetworksByAdapters(hostInfo, vm.GetList(x=>x.NetworkAdapters))
                }).ToEither();
        }

        private EitherAsync<Error, VirtualMachineCpuData> GetCpuData(TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            return _engine.GetObjectsAsync<VirtualMachineCpuData>(
                    new PsCommandBuilder().AddInput(vmInfo.PsObject).AddCommand("Get-VMProcessor"))
                .ToError().ToAsync().Bind(
                    r => r.HeadOrLeft(Error.New(Errors.SequenceEmpty)).ToAsync())
                .Map(r => r.ToValue());
        }

        private EitherAsync<Error, VirtualMachineMemoryData> GetMemoryData(TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            return _engine.GetObjectsAsync<VirtualMachineMemoryData>(
                    new PsCommandBuilder().AddInput(vmInfo.PsObject).AddCommand("Get-VMMemory"))
                .ToError().ToAsync().Bind(
                    r => r.HeadOrLeft(Error.New(Errors.SequenceEmpty)).ToAsync())
                .Map(r => r.ToValue());
        }

        private EitherAsync<Error, VirtualMachineFirmwareData> GetFirmwareData(TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            if (vmInfo.Value.Generation == 1)
                return Prelude.RightAsync<Error, VirtualMachineFirmwareData>(new VirtualMachineFirmwareData());

            return _engine.GetObjectsAsync<VMFirmwareInfo>(
                    new PsCommandBuilder().AddInput(vmInfo.PsObject).AddCommand("Get-VMFirmware"))
                .ToError().ToAsync().Map(
                    r => r.HeadOrNone())
                .Map(r => r.Match(
                    None: () => new VirtualMachineFirmwareData(),
                    Some: info => new VirtualMachineFirmwareData
                    {
                        SecureBoot = info.Value.SecureBoot == OnOffState.On,
                        SecureBootTemplate = info.Value.SecureBootTemplate
                    }
                    
                    ));
        }

        private static DiskInfo CreateDiskInfo(DiskStorageSettings storageSettings)
        {
            var disk = new DiskInfo
            {
                Id = Guid.NewGuid(),
                Name = storageSettings.Name,
                Path = storageSettings.Path,
                FileName = storageSettings.FileName,
                SizeBytes = storageSettings.SizeBytes
            };

            storageSettings.StorageIdentifier.IfSome(n => disk.StorageIdentifier = n);
            storageSettings.StorageNames.DataStoreName.IfSome(n => disk.DataStore = n);
            storageSettings.StorageNames.ProjectName.IfSome(n => disk.ProjectName = n);
            storageSettings.StorageNames.EnvironmentName.IfSome(n => disk.Environment = n);
            storageSettings.StorageIdentifier.IfNone(() => disk.Frozen = true);
            storageSettings.ParentSettings.IfSome(parentSettings => disk.Parent = CreateDiskInfo(parentSettings));
            return disk;
        }

        private static Guid GetMetadataId(TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var notes = vmInfo.Value.Notes;

            var metadataIdString = "";
            if (!string.IsNullOrWhiteSpace(notes))
            {
                var metadataIndex = notes.IndexOf("Eryph metadata id: ", StringComparison.InvariantCultureIgnoreCase);
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
            Seq<CurrentHardDiskDriveStorageSettings> storageSettings, IEnumerable<TypedPsObject<VirtualMachineDeviceInfo>> hdDevices)
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

                if (storageSetting != null)
                    drive.Disk = CreateDiskInfo(storageSetting.DiskSettings);

                yield return drive;
            }
        }

    }
}