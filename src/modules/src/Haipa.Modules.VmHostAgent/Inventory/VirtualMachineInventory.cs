using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Haipa.Primitives;
using Haipa.Primitives.Resources.Disks;
using Haipa.Primitives.Resources.Machines;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Core;
using Haipa.VmManagement.Networking;
using Haipa.VmManagement.Storage;
using LanguageExt;

namespace Haipa.Modules.VmHostAgent.Inventory
{
    internal class HostInventory
    {
        private readonly IPowershellEngine _engine;

        public HostInventory(IPowershellEngine engine)
        {
            _engine = engine;
        }

        public async Task<Either<PowershellFailure, VMHostMachineData>> InventorizeHost()
        {
            var standardSwitchId = Guid.Parse("c08cb7b8-9b3c-408e-8e30-5e16a3aeb444");

            var res = await (
                from switches in _engine.GetObjectsAsync<dynamic>(new PsCommandBuilder().AddCommand("get-VMSwitch"))
                    .ToAsync()
                let standardSwitchName = switches.Where(x => x.Value.Id == standardSwitchId).Map(x=>(string)x.Value.Name).HeadOrNone()
                from switchNames in Prelude
                    .Right<PowershellFailure, string[]>(switches.Select(s => (string) s.Value.Name).ToArray()).ToAsync()

            from adapters1 in switchNames.Map(name =>
                    _engine.GetObjectsAsync<dynamic>(new PsCommandBuilder().AddCommand("Get-VMNetworkAdapter").AddParameter("ManagementOS").AddParameter("SwitchName", name)).ToAsync()).Traverse(l => l)
                let adapters = adapters1.SelectMany(x => x)
                from networks in adapters.Map(a => GetNetworkFromAdapter(a, standardSwitchName)).Traverse(l => l)
                    .Map(x => x.Traverse(l => l)).ToAsync()
                select new VMHostMachineData
                {
                    Name = Environment.MachineName,
                    Switches = switches.Select(s => new VMHostSwitchData
                    {
                        Id = s.Value.Id.ToString()
                    }).ToArray(),
                    Networks = networks.ToArray()
                }).ToEither();

            return res;
        }

        private static Task<Either<PowershellFailure, MachineNetworkData>> GetNetworkFromAdapter(TypedPsObject<dynamic> adapterInfo, Option<string> standardSwitchName)
        {

            var isStandardSwitchAdapter = adapterInfo.Value.SwitchName == standardSwitchName;

            var networkInfo = from adapter in NetworkInterface.GetAllNetworkInterfaces()
                .Find(x => x.Id == (string)adapterInfo.Value.DeviceId)
                .ToEither(new PowershellFailure{Message = $"Could not find host network adapter for switch {adapterInfo.Value.VaSwitName}"})

            let networks = adapter.GetIPProperties().UnicastAddresses.Select(x =>
                IPNetwork.Parse(x.Address.ToString(), (byte) x.PrefixLength)).ToArray()

            select new MachineNetworkData
            {
                DefaultGateways = isStandardSwitchAdapter
                    ? networks.Select(x => x.FirstUsable.ToString()).ToArray()
                    : adapter.GetIPProperties().GatewayAddresses.Select(x => x.Address.ToString()).ToArray(),
                DhcpEnabled = isStandardSwitchAdapter || adapter.GetIPProperties().GetIPv4Properties().IsDhcpEnabled,
                DnsServers = isStandardSwitchAdapter
                    ? networks.Select(x => x.FirstUsable.ToString()).ToArray()
                    : adapter.GetIPProperties().DnsAddresses.Select(x => x.ToString()).ToArray(),
                IPAddresses = adapter.GetIPProperties().UnicastAddresses.Select(x => x.Address.ToString()).ToArray(),
                Name = ((string)adapterInfo.Value.SwitchName).Apply(s => Regex.Match(s, @"^([\w\-]+)")).Value.ToLowerInvariant(),
                Subnets = networks.Select(x => x.ToString()).ToArray()
            };

            return networkInfo.ToAsync().ToEither();
        }
    }

    internal class VirtualMachineInventory
    {
        private readonly IPowershellEngine _engine;
        private readonly HostSettings _hostSettings;

        public VirtualMachineInventory(IPowershellEngine engine, HostSettings hostSettings)
        {
            _engine = engine;
            _hostSettings = hostSettings;
        }

        public Task<Either<PowershellFailure, VirtualMachineData>> InventorizeVM(TypedPsObject<VmManagement.Data.Full.VirtualMachineInfo> vmInfo)

        {
            return from vm in Prelude.RightAsync<PowershellFailure, TypedPsObject<VmManagement.Data.Full.VirtualMachineInfo>>(vmInfo).ToEither()
                from diskStorageSettings in CurrentHardDiskDriveStorageSettings.Detect(_engine, _hostSettings, vm.Value.HardDrives)

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
                        Networks = VirtualNetworkQuery.GetNetworksByAdapters(vm.Value.Id, vm.Value.NetworkAdapters)
                    };
        }

        private static DiskInfo CreateDiskInfo(DiskStorageSettings storageSettings)
        {
            var disk = new DiskInfo
            {
                Id = Guid.NewGuid(),
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
        private static Guid GetMetadataId(TypedPsObject<VmManagement.Data.Full.VirtualMachineInfo> vmInfo)
        {
            var notes = vmInfo.Value.Notes;

            var metadataIdString = "";
            if (!string.IsNullOrWhiteSpace(notes))
            {
                var metadataIndex = notes.IndexOf("Haipa metadata id: ", StringComparison.InvariantCultureIgnoreCase);
                if (metadataIndex != -1)
                {
                    var metadataEnd = metadataIndex + "Haipa metadata id: ".Length + 36;
                    if (metadataEnd <= notes.Length)
                        metadataIdString = notes.Substring(metadataIndex + "Haipa metadata id: ".Length, 36);

                }
            }

            return !Guid.TryParse(metadataIdString, out var metadataId) 
                ? Guid.Empty
                : metadataId;
        }


        private static IEnumerable<VirtualMachineDriveData> CreateHardDriveInfo(Seq<CurrentHardDiskDriveStorageSettings> storageSettings, IEnumerable<HardDiskDriveInfo> hds)
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
                    Type = storageSetting?.Type,
                };

                if (storageSetting != null)
                    drive.Disk = CreateDiskInfo(storageSetting.DiskSettings);
                
                yield return drive;

            }
        }
    }
}