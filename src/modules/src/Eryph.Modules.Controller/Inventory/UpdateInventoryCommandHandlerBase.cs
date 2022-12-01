using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Machines.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources.Disks;
using Eryph.Resources.Machines;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.Modules.Controller.Inventory
{
    internal class UpdateInventoryCommandHandlerBase
    {
        private readonly IOperationDispatcher _dispatcher;
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IVirtualDiskDataService _vhdDataService;

        protected readonly IVirtualMachineMetadataService MetadataService;


        protected UpdateInventoryCommandHandlerBase(
            IVirtualMachineMetadataService metadataService, IOperationDispatcher dispatcher,
            IVirtualMachineDataService vmDataService, IVirtualDiskDataService vhdDataService)
        {
            MetadataService = metadataService;
            _dispatcher = dispatcher;
            _vmDataService = vmDataService;
            _vhdDataService = vhdDataService;
        }

        private static void SelectAllParentDisks(ref List<DiskInfo> parentDisks, DiskInfo disk)
        {
            if (disk.Parent != null)
                SelectAllParentDisks(ref parentDisks, disk.Parent);

            parentDisks.Add(disk);
        }

        protected async Task UpdateVMs(IEnumerable<VirtualMachineData> vmList, VirtualCatletHost hostMachine)
        {
            
            //var vms = vmList as VirtualMachineData[] ?? vmList.ToArray();

            //var diskInfos = vms.SelectMany(x => x.Drives.Select(d => d.Disk)).ToList();
            //var allDisks = new List<DiskInfo>();
            //foreach (var diskInfo in diskInfos) SelectAllParentDisks(ref allDisks, diskInfo);

            //diskInfos = allDisks.Distinct((x, y) =>
            //    string.Equals(x.Path, y.Path, StringComparison.InvariantCultureIgnoreCase) &&
            //    string.Equals(x.FileName, y.FileName, StringComparison.InvariantCultureIgnoreCase)).ToList();

            //var addedDisks = new List<VirtualDisk>();

            //async Task<Option<VirtualDisk>> LookupVirtualDisk(DiskInfo diskInfo)
            //{
            //    var disksDataCandidates = await _vhdDataService.FindVHDByLocation(
            //            diskInfo.DataStore,
            //            diskInfo.Project,
            //            diskInfo.Environment,
            //            diskInfo.StorageIdentifier,
            //            diskInfo.Name)
            //        .Map(l => addedDisks.Append(l))
            //        .Map(l => l.Filter(
            //            x => x.DataStore == diskInfo.DataStore &&
            //                 x.Project == diskInfo.Project &&
            //                 x.Environment == diskInfo.Environment &&
            //                 x.StorageIdentifier == diskInfo.StorageIdentifier &&
            //                 x.Name == diskInfo.Name))
            //        .Map(x => x.ToArray());


            //    Option<VirtualDisk> disk;
            //    if (disksDataCandidates.Length <= 1)
            //        disk = disksDataCandidates.FirstOrDefault() ?? Option<VirtualDisk>.None;
            //    else
            //        disk = disksDataCandidates.FirstOrDefault(x =>
            //            string.Equals(x.Path, diskInfo.Path, StringComparison.InvariantCultureIgnoreCase) &&
            //            string.Equals(x.FileName, diskInfo.FileName, StringComparison.InvariantCultureIgnoreCase)) 
            //               ?? Option<VirtualDisk>.None;

            //    return disk;
            //}

            //foreach (var diskInfo in diskInfos)
            //{
            //    var disk = await LookupVirtualDisk(diskInfo)
            //        .IfNoneAsync(async () =>
            //    {
            //        var d = new VirtualDisk
            //        {
            //            Id = diskInfo.Id,
            //            Name = diskInfo.Name,
            //            DataStore = diskInfo.DataStore,
            //            Project = diskInfo.Project,
            //            Environment = diskInfo.Environment,
            //            StorageIdentifier = diskInfo.StorageIdentifier
            //        };
            //        d = await _vhdDataService.AddNewVHD(d);
            //        addedDisks.Add(d);
            //        return d;
            //    });

            //    diskInfo.Id = disk.Id; // copy id of existing record
            //    disk.FileName = diskInfo.FileName;
            //    disk.Path = diskInfo.Path;
            //    disk.SizeBytes = diskInfo.SizeBytes; 
            //    await _vhdDataService.UpdateVhd(disk);

            //}

            ////second loop to assign parents and to update state db
            //foreach (var diskInfo in diskInfos)
            //{
            //    await LookupVirtualDisk(diskInfo).IfSomeAsync(async currentDisk =>
            //   {
            //       if (diskInfo.Parent == null)
            //       {
            //           currentDisk.Parent = null;
            //           return;
            //       }

            //       await LookupVirtualDisk(diskInfo.Parent)
            //           .IfSomeAsync(parentDisk =>
            //           {
            //               currentDisk.Parent = parentDisk;

            //           });
            //       await _vhdDataService.UpdateVhd(currentDisk);

            //   });
            //}


            //foreach (var vmInfo in vms)
            //{
            //    //get known metadata for VM, if metadata is unknown skip this VM as it is not in Eryph management
            //    var optionalMetadata = await MetadataService.GetMetadata(vmInfo.MetadataId);
            //    //TODO: add logging that entry has been skipped due to missing metadata

            //    optionalMetadata.IfSome(async metadata =>
            //    {
            //        var optionalMachine = (await _vmDataService.GetVM(metadata.MachineId));

            //        //machine not found or metadata is assigned to new VM - a new VM resource will be created)
            //        if (optionalMachine.IsNone || metadata.VMId != vmInfo.VMId)
            //        {
            //            // create new metadata for machines that have been imported
            //            if (metadata.VMId != vmInfo.VMId)
            //            {
            //                var oldMetadataId = metadata.Id;
            //                metadata.Id = Guid.NewGuid();
            //                metadata.MachineId = Guid.NewGuid();
            //                metadata.VMId = vmInfo.VMId;

            //                await _dispatcher.StartNew(new UpdateVirtualMachineMetadataCommand
            //                {
            //                    AgentName = hostMachine.AgentName,
            //                    CurrentMetadataId = oldMetadataId,
            //                    NewMetadataId = metadata.Id,
            //                    VMId = vmInfo.VMId,
            //                });
            //            }

            //            if (metadata.MachineId == Guid.Empty)
            //                metadata.MachineId = Guid.NewGuid();

            //            await _vmDataService.AddNewVM(
            //                VirtualMachineInfoToMachine(vmInfo, hostMachine, metadata.MachineId),
            //                metadata);


            //            return;
            //        }

            //        optionalMachine.IfSome(existingMachine =>
            //        {
            //            // update data for existing machine
            //            var newMachine = VirtualMachineInfoToMachine(vmInfo, hostMachine, existingMachine.Id);
            //            existingMachine.Name = newMachine.Name;
            //            existingMachine.Status = newMachine.Status;
            //            existingMachine.Host = hostMachine;
            //            existingMachine.AgentName = newMachine.AgentName;

            //            //MergeMachineNetworks(newMachine.ReportedNetworks, existingMachine);

            //            existingMachine.NetworkAdapters = newMachine.NetworkAdapters;
            //            existingMachine.Drives = newMachine.Drives;
            //        });


            //    });
            //}
        }

        private VirtualCatlet VirtualMachineInfoToMachine(VirtualMachineData vmInfo, VirtualCatletHost hostMachine,
            Guid machineId)
        {
            return new VirtualCatlet
            {
                Id = machineId,
                VMId = vmInfo.VMId,
                Name = vmInfo.Name,
                Status = MapVmStatusToMachineStatus(vmInfo.Status),
                Host = hostMachine,
                AgentName = hostMachine.AgentName,
                MetadataId = vmInfo.MetadataId,
                UpTime = vmInfo.UpTime,
                NetworkAdapters = vmInfo.NetworkAdapters.Select(a => new VirtualCatletNetworkAdapter
                {
                    Id = a.Id,
                    MachineId = machineId,
                    Name = a.AdapterName,
                    SwitchName = a.VirtualSwitchName
                }).ToList(),
                Drives = vmInfo.Drives.Select(d => new VirtualMachineDrive
                {
                    Id = d.Id,
                    MachineId = machineId,
                    Type = d.Type,
                    //TODO: this code may needs to be improved
                    AttachedDisk = d.Disk != null
                        ? _vhdDataService.GetVHD(d.Disk.Id).GetAwaiter().GetResult().IfNoneUnsafe(() => null) : null

                }).ToList(),
                ReportedNetworks = (vmInfo.Networks?.ToReportedNetwork(machineId) ?? Array.Empty<ReportedNetwork>()).ToList()
            };
        }

        private static CatletStatus MapVmStatusToMachineStatus(VmStatus status)
        {
            switch (status)
            {
                case VmStatus.Stopped:
                    return CatletStatus.Stopped;
                case VmStatus.Running:
                    return CatletStatus.Running;
                case VmStatus.Pending:
                    return CatletStatus.Pending;
                case VmStatus.Error:
                    return CatletStatus.Error;

                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }


        protected static void MergeMachineNetworks(IEnumerable<VirtualNetwork> newNetworks, Catlet existingMachine)
        {
            //merge Networks 
            //var networkList = newNetworks.ToList();
            //var existingNetworksList = existingMachine.Networks?.ToList() ?? new List<MachineNetwork>();

            //var existingNetworksListUniqueName =
            //    existingNetworksList.Distinct((x, y) => x.Name == y.Name).ToArray();

            //foreach (var existingNetwork in existingNetworksListUniqueName)
            //{
            //    var networksWithSameName =
            //        existingNetworksList.Where(x => x.Name == existingNetwork.Name).ToArray();

            //    var deleteNetwork = networksWithSameName.Length > 1 || //delete if name is not unique
            //                                                           //delete also, if there is a reference in current machine but no longer in new machine
            //                        existingNetworksList.All(x => x.Name != existingNetwork.Name);

            //    if (deleteNetwork)
            //        existingNetworksList.RemoveAll(x => x.Name == existingNetwork.Name);
            //}

            //foreach (var newNetwork in networkList.ToArray())
            //{
            //    var existingNetwork = existingNetworksList.FirstOrDefault(x => x.Name == newNetwork.Name);
            //    if (existingNetwork == null) continue;
            //    existingNetwork.DnsServerAddresses = newNetwork.DnsServerAddresses;
            //    existingNetwork.IPv4DefaultGateway = newNetwork.IPv4DefaultGateway;
            //    existingNetwork.IPv4DefaultGateway = newNetwork.IPv6DefaultGateway;
            //    existingNetwork.IpV4Addresses = newNetwork.IpV4Addresses;
            //    existingNetwork.IpV6Addresses = newNetwork.IpV6Addresses;
            //    existingNetwork.IpV4Subnets = newNetwork.IpV4Subnets;
            //    existingNetwork.IpV6Subnets = newNetwork.IpV6Subnets;

            //    networkList.Remove(newNetwork);
            //}

            //existingNetworksList.AddRange(networkList);
            //existingMachine.Networks = existingNetworksList;
        }
    }
}