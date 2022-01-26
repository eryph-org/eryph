using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Eryph.Resources.Disks;
using Eryph.Resources.Machines;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Networking;
using Eryph.VmManagement.Storage;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Eryph.Modules.VmHostAgent.Inventory
{
    internal class HostInventory
    {
        private readonly IPowershellEngine _engine;
        private readonly ILogger _log;
        private readonly Guid _standardSwitchId = Guid.Parse("c08cb7b8-9b3c-408e-8e30-5e16a3aeb444");

        public HostInventory(IPowershellEngine engine, ILogger log)
        {
            _engine = engine;
            _log = log;
        }

        public async Task<Either<PowershellFailure, VMHostMachineData>> InventorizeHost()
        {

            var res = await (
                from switches in _engine.GetObjectsAsync<dynamic>(new PsCommandBuilder().AddCommand("get-VMSwitch"))
                    .ToAsync()
                let standardSwitchName = switches.Where(x => x.Value.Id == _standardSwitchId)
                    .Map(x => (string)x.Value.Name).HeadOrNone()
                from switchNames in Prelude
                    .Right<PowershellFailure, string[]>(switches.Select(s => (string)s.Value.Name).ToArray()).ToAsync()
                from adaptersSeq in switchNames.Map(name =>

                        _engine.GetObjectsAsync<dynamic>(new PsCommandBuilder().AddCommand("Get-VMNetworkAdapter")
                            .AddParameter("ManagementOS").AddParameter("SwitchName", name)).ToAsync())
                    .TraverseParallel(l => l)
                let adaptersFromSwitches = adaptersSeq.SelectMany(x => x)
                let standardSwitchAdapter = FindStandardSwitchAdapter(standardSwitchName, adaptersFromSwitches)
                let virtualNetworks = adaptersFromSwitches.Map(a => 
                    GetVirtualNetworkFromAdapter(a,IsStandardSwitchAdapter((string)a.Value.Id, standardSwitchAdapter)))
                    .Map(o => o.AsEnumerable()).Flatten()
                let hostNetworks = GetAllHostNetworks(virtualNetworks, standardSwitchAdapter)
                select new VMHostMachineData
                {
                    Name = Environment.MachineName,
                    Switches = switches.Select(s => new VMHostSwitchData
                    {
                        Id = s.Value.Id.ToString()
                    }).ToArray(),
                    VirtualNetworks = virtualNetworks.ToArray(),
                    Networks = hostNetworks.ToArray(),
                    HardwareId = GetHostUuid() ?? GetHostMachineGuid()
                }).ToEither();

            return res;
        }

        private IEnumerable<MachineNetworkData> GetAllHostNetworks(IEnumerable<HostVirtualNetworkData> virtualNetworks, Option<TypedPsObject<object>> standardSwitchAdapter)
        {
            return NetworkInterface.GetAllNetworkInterfaces().Map(nwInterface =>
            {
                var (name, isStandardSwitch) =
                    virtualNetworks.Find(virtualNetwork => virtualNetwork.DeviceId == nwInterface.Id)
                        .Match(virtualNetwork => (virtualNetwork.Name,
                            IsStandardSwitchAdapter(virtualNetwork.AdapterId, standardSwitchAdapter)),
                            () => (nwInterface.Name, false) );

                return NetworkInterfaceToMachineNetworkData<MachineNetworkData>(nwInterface, isStandardSwitch, name);
            });
        }


        private Option<HostVirtualNetworkData> GetVirtualNetworkFromAdapter(
            TypedPsObject<dynamic> adapterInfo, bool isStandardSwitchAdapter)
        {
            if (adapterInfo.Value.SwitchId == _standardSwitchId && !isStandardSwitchAdapter)
                return Option<HostVirtualNetworkData>.None;

            //TODO: needs abstraction for testing
            var networkInfo = from nwInterface in NetworkInterface.GetAllNetworkInterfaces()
                .Find(x =>
                    {
                        var stats = x.GetIPStatistics();
                        return x.Id == (string)adapterInfo.Value.DeviceId;
                    })
                    .Map(Option<NetworkInterface>.Some)
                    .IfNone(() =>
                    {
                        _log.LogWarning(
                            $"Could not find host network adapter for switch '{adapterInfo.Value.SwitchName}', DeviceId: '{adapterInfo.Value.DeviceId}').");
                        return Option<NetworkInterface>.None;
                    })
                let networkName = ((string)adapterInfo.Value.SwitchName).Apply(s => Regex.Match(s, @"^([\w\-]+)")).Value
                let network =
                    NetworkInterfaceToMachineNetworkData<HostVirtualNetworkData>(nwInterface, isStandardSwitchAdapter,
                        networkName)
                select network.Apply(n =>
                {
                    n.VirtualSwitchName = adapterInfo.Value.SwitchName;
                    n.DeviceId = (string)adapterInfo.Value.DeviceId;
                    n.AdapterId = (string)adapterInfo.Value.Id;
                    return n;
                });   
                   

            return networkInfo;
        }


        private Option<TypedPsObject<dynamic>> FindStandardSwitchAdapter(Option<string> standardSwitchName,
            IEnumerable<TypedPsObject<dynamic>> adapters)
        {
            var swAdapters = adapters.Where(a => a.Value.SwitchName == standardSwitchName).ToList();

            if (swAdapters.Length() > 1)
            {
                var adapterNames = string.Join(',', swAdapters.Select(x => (string)x.Value.Name));
                _log.LogDebug("Multiple candidates found for standard switch port. Port candidates: {adapterNames}", adapterNames);

                // this is a dirty hack to filter out ports from Sandbox / Windows Containers
                // at least for Sandbox additional ports are on the standard switch that have to be ignored
                // these ports all have additional properties, so we look for a port without additional properties in registry

                //TODO: needs abstraction for testing
                foreach (var adapter in swAdapters.ToArray())
                {
                    var portPath = ((string)adapter.Value.Id).Replace("Microsoft:", "");
                    var subKeys =
                        Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\vmsmp\parameters\SwitchList\{portPath}")?.GetSubKeyNames();

                    var adapterName = (string)adapter.Value.Name;

                    if (subKeys is { Length: > 0 })
                    {
                        _log.LogTrace("Adapter '{adapterName}' has additional port properties - ignored", adapterName);
                        swAdapters.Remove(adapter);
                    }
                    else
                    {
                        _log.LogTrace("Adapter '{adapterName}' has no additional port properties - accept it as standard switch port.", adapterName);

                    }
                }
            }

            if (swAdapters.Length() > 1)
            {
                var adapterNames = string.Join(',', swAdapters.Select(x => (string)x.Value.Name));
                _log.LogWarning("Multiple candidates found for standard switch port. Choosing first port. Port candidates: {adapterNames}", adapterNames);

            }

            return swAdapters.HeadOrNone();
        }

        private static bool IsStandardSwitchAdapter(string adapterId,
            Option<TypedPsObject<dynamic>> standardSwitchAdapter)
        {
            return standardSwitchAdapter.Map(sa => adapterId == sa.Value.Id)
                .Match(s => (bool)s, () => false);

        }

        private static T NetworkInterfaceToMachineNetworkData<T>(NetworkInterface networkInterface,
            bool isStandardSwitchAdapter, string name) where T: MachineNetworkData, new()
        {
            var networks = networkInterface.GetIPProperties().UnicastAddresses.Select(x =>
                IPNetwork.Parse(x.Address.ToString(), (byte)x.PrefixLength)).ToArray();

            return new T
            {
                DefaultGateways = isStandardSwitchAdapter
                    ? networks.Select(x => x.FirstUsable.ToString()).ToArray()
                    : networkInterface.GetIPProperties().GatewayAddresses.Select(x => x.Address.ToString()).ToArray(),
                DhcpEnabled =
                    isStandardSwitchAdapter || networkInterface.GetIPProperties().GetIPv4Properties().IsDhcpEnabled,
                DnsServers = isStandardSwitchAdapter
                    ? networks.Select(x => x.FirstUsable.ToString()).ToArray()
                    : networkInterface.GetIPProperties().DnsAddresses.Select(x => x.ToString()).ToArray(),
                IPAddresses =
                    networkInterface.GetIPProperties().UnicastAddresses.Select(x => x.Address.ToString()).ToArray(),
                Name = name
                    .ToLowerInvariant(),
                Subnets = networks.Select(x => x.ToString()).ToArray()

            };
        }


        private static string GetHostUuid()
        {
            var uiid = "";
            try
            {
                var uuidSearcher = new ManagementObjectSearcher("SELECT UUId FROM Win32_ComputerSystemProduct");
                foreach (var uuidSearcherResult in uuidSearcher.Get())
                {
                    uiid = uuidSearcherResult["UUId"] as string;

                    if (uiid == "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")
                        uiid = null;
                    break;
                }

            }
            catch (Exception)
            {
                // ignored
            }

            return uiid;
        }

        private static string GetHostMachineGuid()
        {
            var guid = "";
            try
            {
                using (var registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    if (registryKey != null) guid = registryKey.GetValue("MachineGuid") as string;
                }
            }
            catch (Exception)
            {
                // ignored
            }

            return guid;
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

        public Task<Either<PowershellFailure,VirtualMachineData>> InventorizeVM(
            TypedPsObject<VirtualMachineInfo> vmInfo)

        {
           return from vm in Prelude.RightAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo)
                    .ToEither()
                from diskStorageSettings in CurrentHardDiskDriveStorageSettings.Detect(_engine, _hostSettings,
                    vm.Value.HardDrives)
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