using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Resources.Machines.Commands;
using Haipa.Modules.Controller.IdGenerator;
using Haipa.Modules.Controller.Operations;
using Haipa.Resources.Disks;
using Haipa.Resources.Machines;
using Haipa.StateDb;
using Haipa.StateDb.Model;

namespace Haipa.Modules.Controller.Inventory
{
    internal class UpdateInventoryCommandHandlerBase
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;
        private readonly IVirtualMachineDataService _vmDataService;
        protected readonly Id64Generator IdGenerator;

        protected readonly IVirtualMachineMetadataService MetadataService;
        protected readonly StateStoreContext StateStoreContext;


        protected UpdateInventoryCommandHandlerBase(StateStoreContext stateStoreContext, Id64Generator idGenerator,
            IVirtualMachineMetadataService metadataService, IOperationTaskDispatcher taskDispatcher,
            IVirtualMachineDataService vmDataService)
        {
            StateStoreContext = stateStoreContext;
            IdGenerator = idGenerator;
            MetadataService = metadataService;
            _taskDispatcher = taskDispatcher;
            _vmDataService = vmDataService;
        }

        private static void SelectAllParentDisks(ref List<DiskInfo> parentDisks, DiskInfo disk)
        {
            if (disk.Parent != null)
                SelectAllParentDisks(ref parentDisks, disk.Parent);

            parentDisks.Add(disk);
        }

        protected async Task UpdateVMs(IEnumerable<VirtualMachineData> vmList, VMHostMachine hostMachine)
        {
            var vms = vmList as VirtualMachineData[] ?? vmList.ToArray();

            var diskInfos = vms.SelectMany(x => x.Drives.Select(d => d.Disk)).ToList();
            var allDisks = new List<DiskInfo>();
            foreach (var diskInfo in diskInfos) SelectAllParentDisks(ref allDisks, diskInfo);

            diskInfos = allDisks.Distinct((x, y) =>
                string.Equals(x.Path, y.Path, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(x.FileName, y.FileName, StringComparison.InvariantCultureIgnoreCase)).ToList();

            var addedDisks = new List<VirtualDisk>();

            VirtualDisk LookupVirtualDisk(DiskInfo diskInfo)
            {
                var disksDataCandidates = StateStoreContext.VirtualDisks.Where(
                        x => x.DataStore == diskInfo.DataStore &&
                             x.Project == diskInfo.Project &&
                             x.Environment == diskInfo.Environment &&
                             x.StorageIdentifier == diskInfo.StorageIdentifier &&
                             x.Name == diskInfo.Name).AsEnumerable()
                    .Append(addedDisks
                        .Where(
                            x => x.DataStore == diskInfo.DataStore &&
                                 x.Project == diskInfo.Project &&
                                 x.Environment == diskInfo.Environment &&
                                 x.StorageIdentifier == diskInfo.StorageIdentifier &&
                                 x.Name == diskInfo.Name)
                    ).ToArray();

                VirtualDisk disk;
                if (disksDataCandidates.Length <= 1)
                    disk = disksDataCandidates.FirstOrDefault();
                else
                    disk = disksDataCandidates.FirstOrDefault(x =>
                        string.Equals(x.Path, diskInfo.Path, StringComparison.InvariantCultureIgnoreCase) &&
                        string.Equals(x.FileName, diskInfo.FileName, StringComparison.InvariantCultureIgnoreCase));

                return disk;
            }

            foreach (var diskInfo in diskInfos)
            {
                var disk = LookupVirtualDisk(diskInfo);
                if (disk == null)
                {
                    disk = new VirtualDisk
                    {
                        Id = diskInfo.Id,
                        DataStore = diskInfo.DataStore,
                        Project = diskInfo.Project,
                        Environment = diskInfo.Environment,
                        StorageIdentifier = diskInfo.StorageIdentifier
                    };
                    await StateStoreContext.VirtualDisks.AddAsync(disk);
                    addedDisks.Add(disk);
                }

                diskInfo.Id = disk.Id; // copy id of existing record
                disk.Name = diskInfo.Name;
                disk.FileName = diskInfo.FileName;
                disk.Path = diskInfo.Path;
                disk.SizeBytes = diskInfo.SizeBytes;
            }

            //second loop to assign parents
            foreach (var diskInfo in diskInfos)
            {
                var currentDisk = LookupVirtualDisk(diskInfo);
                if (currentDisk == null)
                    continue; // should not happen

                if (diskInfo.Parent == null)
                {
                    currentDisk.Parent = null;
                    continue;
                }

                var parentDisk = LookupVirtualDisk(diskInfo.Parent);
                currentDisk.Parent = parentDisk;
            }

            foreach (var vmInfo in vms)
            {
                //get known metadata for VM, if metadata is unknown skip this VM as it is not in Haipa management
                var optionalMetadata = await MetadataService.GetMetadata(vmInfo.MetadataId);
                //TODO: add logging that entry has been skipped due to missing metadata

                optionalMetadata.IfSome(async metadata =>
                {
                    var existingMachine = await StateStoreContext.VirtualMachines.FindAsync(metadata.MachineId);

                    //machine not found or metadata is assigned to new VM - a new VM resource will be created)
                    if (existingMachine == null || metadata.VMId != vmInfo.VMId)
                    {
                        // create new metadata for machines that have been imported
                        if (metadata.VMId != vmInfo.VMId)
                        {
                            var oldMetadataId = metadata.Id;
                            metadata.Id = Guid.NewGuid();
                            metadata.MachineId = IdGenerator.GenerateId();
                            metadata.VMId = vmInfo.VMId;

                            await _taskDispatcher.Send(new UpdateVirtualMachineMetadataCommand
                            {
                                AgentName = hostMachine.AgentName,
                                CurrentMetadataId = oldMetadataId,
                                NewMetadataId = metadata.Id,
                                VMId = vmInfo.VMId,
                                OperationId = Guid.NewGuid(),
                                TaskId = Guid.NewGuid()
                            });
                        }

                        if (metadata.MachineId == 0)
                            metadata.MachineId = IdGenerator.GenerateId();

                        await _vmDataService.AddNewVM(
                            VirtualMachineInfoToMachine(vmInfo, hostMachine, metadata.MachineId),
                            metadata);


                        return;
                    }

                    // update data for existing machine
                    var newMachine = VirtualMachineInfoToMachine(vmInfo, hostMachine, existingMachine.Id);
                    existingMachine.Name = newMachine.Name;
                    existingMachine.Status = newMachine.Status;
                    existingMachine.Host = hostMachine;
                    existingMachine.AgentName = newMachine.AgentName;

                    existingMachine.Networks ??= new List<MachineNetwork>();
                    MergeMachineNetworks(newMachine, existingMachine);

                    existingMachine.NetworkAdapters = newMachine.NetworkAdapters;
                    existingMachine.Drives = newMachine.Drives;
                });
            }
        }

        private VirtualMachine VirtualMachineInfoToMachine(VirtualMachineData vmInfo, VMHostMachine hostMachine,
            long machineId)
        {
            return new VirtualMachine
            {
                Id = machineId,
                VMId = vmInfo.VMId,
                Name = vmInfo.Name,
                Status = MapVmStatusToMachineStatus(vmInfo.Status),
                Host = hostMachine,
                AgentName = hostMachine.AgentName,
                MetadataId = vmInfo.MetadataId,
                NetworkAdapters = vmInfo.NetworkAdapters.Select(a => new VirtualMachineNetworkAdapter
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
                    AttachedDisk = d.Disk != null ? StateStoreContext.Find<VirtualDisk>(d.Disk.Id) : null
                }).ToList(),
                Networks = vmInfo.Networks?.ToMachineNetwork(machineId).ToList()
            };
        }

        private static MachineStatus MapVmStatusToMachineStatus(VmStatus status)
        {
            switch (status)
            {
                case VmStatus.Stopped:
                    return MachineStatus.Stopped;
                case VmStatus.Running:
                    return MachineStatus.Running;
                case VmStatus.Pending:
                    return MachineStatus.Pending;
                case VmStatus.Error:
                    return MachineStatus.Error;

                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }


        protected static void MergeMachineNetworks(Machine newMachine, Machine existingMachine)
        {
            //merge Networks 
            var networkList = newMachine.Networks.ToList();
            var existingNetworksListUniqueName = existingMachine.Networks.ToList().Distinct((x, y) => x.Name == y.Name);

            foreach (var existingNetwork in existingNetworksListUniqueName)
            {
                var networksWithSameName =
                    existingMachine.Networks.Where(x => x.Name == existingNetwork.Name).ToArray();

                var deleteNetwork = networksWithSameName.Length > 1 || //delete if name is not unique
                                    //delete also, if there is a reference in current machine but no longer in new machine
                                    existingMachine.Networks.All(x => x.Name != existingNetwork.Name);

                if (deleteNetwork)
                    existingMachine.Networks.RemoveAll(x => x.Name == existingNetwork.Name);
            }

            foreach (var newNetwork in networkList.ToArray())
            {
                var existingNetwork = existingMachine.Networks.FirstOrDefault(x => x.Name == newNetwork.Name);
                if (existingNetwork == null) continue;
                existingNetwork.DnsServerAddresses = newNetwork.DnsServerAddresses;
                existingNetwork.IPv4DefaultGateway = newNetwork.IPv4DefaultGateway;
                existingNetwork.IPv4DefaultGateway = newNetwork.IPv6DefaultGateway;
                existingNetwork.IpV4Addresses = newNetwork.IpV4Addresses;
                existingNetwork.IpV6Addresses = newNetwork.IpV6Addresses;
                existingNetwork.IpV4Subnets = newNetwork.IpV4Subnets;
                existingNetwork.IpV6Subnets = newNetwork.IpV6Subnets;

                networkList.Remove(newNetwork);
            }

            existingMachine.Networks.AddRange(networkList);
        }
    }
}