using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Events;
using Haipa.Modules.VmHostAgent.Inventory;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Core;
using Haipa.VmManagement.Data.Full;
using Haipa.VmManagement.Networking;
using Haipa.VmManagement.Storage;
using LanguageExt;

namespace Haipa.Modules.VmHostAgent
{
    internal class VirtualMachineInventory
    {
        private readonly IPowershellEngine _engine;
        private readonly HostSettings _hostSettings;

        public VirtualMachineInventory(IPowershellEngine engine, HostSettings hostSettings)
        {
            _engine = engine;
            _hostSettings = hostSettings;
        }

        public Task<Either<PowershellFailure, MachineInfo>> InventorizeVM(TypedPsObject<VirtualMachineInfo> vmInfo)

        {
            return from vm in Prelude.RightAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo).ToEither()
                from diskStorageSettings in CurrentHardDiskDriveStorageSettings.Detect(_engine, _hostSettings, vm.Value.HardDrives)
                        
                    select new MachineInfo
                    {
                        MachineId = vm.Value.Id,
                        Status = InventoryConverter.MapVmInfoStatusToVmStatus(vm.Value.State),
                        Name = vm.Value.Name,
                        Drives = CreateHardDriveInfo(diskStorageSettings, vmInfo.Value.HardDrives).ToArray(),
                        NetworkAdapters = vm.Value.NetworkAdapters?.Map(a => new VirtualMachineNetworkAdapterInfo
                        {
                            Id = a.Id,
                            AdapterName = a.Name,
                            VirtualSwitchName = a.SwitchName
                        }).ToArray(),
                        Networks = VirtualNetworkQuery.GetNetworksByAdapters(vm.Value.Id, vm.Value.NetworkAdapters)
                    };
        }

        private static DiskInfo CreateDiskInfo(DiskStorageSettings storageSettings)
        {
            var disk = new DiskInfo
            {
                Name = storageSettings.Name,
                Path = storageSettings.Path,
                FileName = storageSettings.FileName,
                SizeBytes = storageSettings.SizeBytes,
            };

            storageSettings.StorageIdentifier.IfSome(n => disk.StorageIdentifier = n);
            storageSettings.StorageNames?.DataStoreName.IfSome(n => disk.DataStore = n);
            storageSettings.StorageNames?.ProjectName.IfSome(n => disk.Project = n);
            storageSettings.StorageNames?.EnvironmentName.IfSome(n => disk.Environment = n);
            storageSettings.ParentSettings.IfSome(parentSettings => disk.Parent = CreateDiskInfo(parentSettings));
            return disk;

        }

        private static IEnumerable<VirtualMachineDriveInfo> CreateHardDriveInfo(Seq<CurrentHardDiskDriveStorageSettings> storageSettings, IEnumerable<HardDiskDriveInfo> hds)
        {
            foreach (var hd in hds)
            {
                var storageSetting = storageSettings.FirstOrDefault(x => x.AttachedVMId == hd.Id);

                var drive = new VirtualMachineDriveInfo
                {
                    Id = hd.Id,
                    ControllerLocation = hd.ControllerLocation,
                    ControllerNumber = hd.ControllerNumber,
                    ControllerType = hd.ControllerType,
                    Frozen = storageSetting?.Frozen ?? true,
                    Type = storageSetting?.Type,
                };

                if (storageSetting != null)
                    drive.Disk = CreateDiskInfo(storageSetting.DiskSettings);
                
                yield return drive;

            }
        }
    }
}