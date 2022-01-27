using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Resources.Disks;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using Eryph.VmManagement.Storage;
using LanguageExt;

namespace Eryph.VmManagement.Inventory
{
    public class VirtualMachineInventory
    {
        private readonly IPowershellEngine _engine;
        private readonly HostSettings _hostSettings;
        private readonly IHostInfoProvider _hostInfoProvider;

        public VirtualMachineInventory(IPowershellEngine engine, HostSettings hostSettings, IHostInfoProvider hostInfoProvider)
        {
            _engine = engine;
            _hostSettings = hostSettings;
            _hostInfoProvider = hostInfoProvider;
        }

        public Task<Either<PowershellFailure,VirtualMachineData>> InventorizeVM(
            TypedPsObject<VirtualMachineInfo> vmInfo)

        {
           return (from hostInfo in _hostInfoProvider.GetHostInfoAsync().ToAsync()
               from vm in Prelude.RightAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo)
                    
                from diskStorageSettings in CurrentHardDiskDriveStorageSettings.Detect(_engine, _hostSettings,
                    vm.Value.HardDrives).ToAsync()
                select new VirtualMachineData
                {
                    VMId = vm.Value.Id,
                    MetadataId = GetMetadataId(vm),
                    Status = InventoryConverter.MapVmInfoStatusToVmStatus(vm.Value.State),
                    Name = vm.Value.Name,
                    Drives = CreateHardDriveInfo(diskStorageSettings, vmInfo.Value.HardDrives).ToArray(),
                    NetworkAdapters = vm.Value.NetworkAdapters?.Map(a => new VirtualMachineNetworkAdapterData
                    {
                        Id = a.Id,
                        AdapterName = a.Name,
                        VirtualSwitchName = a.SwitchName
                    }).ToArray(),
                    Networks = VirtualNetworkQuery.GetNetworksByAdapters(vm.Value.Id, hostInfo, vm.Value.NetworkAdapters)
                }).ToEither();
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
            storageSettings.StorageNames?.DataStoreName.IfSome(n => disk.DataStore = n);
            storageSettings.StorageNames?.ProjectName.IfSome(n => disk.Project = n);
            storageSettings.StorageNames?.EnvironmentName.IfSome(n => disk.Environment = n);
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
            Seq<CurrentHardDiskDriveStorageSettings> storageSettings, IEnumerable<HardDiskDriveInfo> hds)
        {
            foreach (var hd in hds)
            {
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