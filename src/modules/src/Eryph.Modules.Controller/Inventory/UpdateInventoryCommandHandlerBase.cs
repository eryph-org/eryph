using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Rebus;
using Eryph.Resources.Disks;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using Rebus.Pipeline;

namespace Eryph.Modules.Controller.Inventory
{
    internal class UpdateInventoryCommandHandlerBase
    {
        private readonly IOperationDispatcher _dispatcher;
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IVirtualDiskDataService _vhdDataService;

        protected readonly IVirtualMachineMetadataService MetadataService;
        protected readonly IStateStore StateStore;
        private readonly IMessageContext _messageContext;

        protected UpdateInventoryCommandHandlerBase(
            IVirtualMachineMetadataService metadataService, IOperationDispatcher dispatcher,
            IVirtualMachineDataService vmDataService, 
            IVirtualDiskDataService vhdDataService,
            IStateStore stateStore, IMessageContext messageContext)
        {
            MetadataService = metadataService;
            _dispatcher = dispatcher;
            _vmDataService = vmDataService;
            _vhdDataService = vhdDataService;
            StateStore = stateStore;
            _messageContext = messageContext;
        }

        private static void SelectAllParentDisks(ref List<DiskInfo> parentDisks, DiskInfo disk)
        {
            if (disk.Parent != null)
                SelectAllParentDisks(ref parentDisks, disk.Parent);

            parentDisks.Add(disk);
        }

        protected async Task UpdateVMs(Guid tenantId, IEnumerable<VirtualMachineData> vmList, CatletFarm hostMachine)
        {

            var vms = vmList as VirtualMachineData[] ?? vmList.ToArray();

            var diskInfos = vms.SelectMany(x => x.Drives.Select(d => d.Disk)).ToList();
            var allDisks = new List<DiskInfo>();
            foreach (var diskInfo in diskInfos) SelectAllParentDisks(ref allDisks, diskInfo);

            diskInfos = allDisks.Distinct((x, y) =>
                string.Equals(x.Path, y.Path, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(x.FileName, y.FileName, StringComparison.InvariantCultureIgnoreCase)).ToList();

            var addedDisks = new List<VirtualDisk>();

            async Task<Option<VirtualDisk>> LookupVirtualDisk(DiskInfo diskInfo)
            {
                var disksDataCandidates = await _vhdDataService.FindVHDByLocation(
                        diskInfo.DataStore,
                        diskInfo.ProjectName,
                        diskInfo.Environment,
                        diskInfo.StorageIdentifier,
                        diskInfo.Name)
                    .Map(l => addedDisks.Append(l))
                    .Map(l => l.Filter(
                        x => x.DataStore == diskInfo.DataStore &&
                             x.Project.Name == diskInfo.ProjectName &&
                             x.Environment == diskInfo.Environment &&
                             x.StorageIdentifier == diskInfo.StorageIdentifier &&
                             x.Name == diskInfo.Name))
                    .Map(x => x.ToArray());

                Option<VirtualDisk> disk;
                if (disksDataCandidates.Length <= 1)
                    disk = disksDataCandidates.FirstOrDefault() ?? Option<VirtualDisk>.None;
                else
                    disk = disksDataCandidates.FirstOrDefault(x =>
                        string.Equals(x.Path, diskInfo.Path, StringComparison.InvariantCultureIgnoreCase) &&
                        string.Equals(x.FileName, diskInfo.FileName, StringComparison.InvariantCultureIgnoreCase))
                           ?? Option<VirtualDisk>.None;

                return disk;
            }

            foreach (var diskInfo in diskInfos)
            {
                var disk = await LookupVirtualDisk(diskInfo)
                    .IfNoneAsync(async () =>
                {

                    var d = new VirtualDisk
                    {
                        Id = diskInfo.Id,
                        Name = diskInfo.Name,
                        DataStore = diskInfo.DataStore,
                        Environment = diskInfo.Environment,
                        Frozen = diskInfo.Frozen,
                        StorageIdentifier = diskInfo.StorageIdentifier,
                        Project = await FindRequiredProject(tenantId,diskInfo.ProjectName)
                    };
                    d = await _vhdDataService.AddNewVHD(d);
                    addedDisks.Add(d);
                    return d;
                });

                diskInfo.Id = disk.Id; // copy id of existing record
                disk.FileName = diskInfo.FileName;
                disk.Path = diskInfo.Path;
                disk.SizeBytes = diskInfo.SizeBytes;
                await _vhdDataService.UpdateVhd(disk);

            }

            //second loop to assign parents and to update state db
            foreach (var diskInfo in diskInfos)
            {
                await LookupVirtualDisk(diskInfo).IfSomeAsync(async currentDisk =>
               {
                   if (diskInfo.Parent == null)
                   {
                       currentDisk.Parent = null;
                       return;
                   }

                   await LookupVirtualDisk(diskInfo.Parent)
                       .IfSomeAsync(parentDisk =>
                       {
                           currentDisk.Parent = parentDisk;

                       });
                   await _vhdDataService.UpdateVhd(currentDisk);

               });
            }


            foreach (var vmInfo in vms)
            {
                //get known metadata for VM, if metadata is unknown skip this VM as it is not in Eryph management
                var optionalMetadata = await MetadataService.GetMetadata(vmInfo.MetadataId);
                //TODO: add logging that entry has been skipped due to missing metadata

                optionalMetadata.IfSome(async metadata =>
                {
                    var optionalMachine = (await _vmDataService.GetVM(metadata.MachineId));

                    //machine not found or metadata is assigned to new VM - a new VM resource will be created)
                    if (optionalMachine.IsNone || metadata.VMId != vmInfo.VMId)
                    {
                        // create new metadata for machines that have been imported
                        if (metadata.VMId != vmInfo.VMId)
                        {
                            var oldMetadataId = metadata.Id;
                            metadata.Id = Guid.NewGuid();
                            metadata.MachineId = Guid.NewGuid();
                            metadata.VMId = vmInfo.VMId;
                            
                            await _dispatcher.StartNew(
                                tenantId,
                                _messageContext.GetTraceId(),
                                new UpdateCatletMetadataCommand
                            {
                                AgentName = hostMachine.Name,
                                CurrentMetadataId = oldMetadataId,
                                NewMetadataId = metadata.Id,
                                CatletId = metadata.MachineId,
                                VMId = vmInfo.VMId,
                            });
                        }

                        if (metadata.MachineId == Guid.Empty)
                            metadata.MachineId = Guid.NewGuid();

                        
                        var project = await FindRequiredProject(tenantId,vmInfo.ProjectName);

                        await _vmDataService.AddNewVM(
                            VirtualMachineInfoToCatlet(vmInfo, hostMachine, metadata.MachineId, project),
                            metadata);


                        return;
                    }

                    optionalMachine.IfSome(existingMachine =>
                    {
                        StateStore.LoadProperty(existingMachine, x=> x.Project);

                        Debug.Assert(existingMachine.Project != null);

                        StateStore.LoadCollection(existingMachine, x => x.ReportedNetworks);
                        StateStore.LoadCollection(existingMachine, x => x.NetworkAdapters);


                        // update data for existing machine
                        var newMachine = VirtualMachineInfoToCatlet(vmInfo,
                            hostMachine, existingMachine.Id, existingMachine.Project);
                        existingMachine.Name = newMachine.Name;
                        existingMachine.Status = newMachine.Status;
                        existingMachine.Host = hostMachine;
                        existingMachine.AgentName = newMachine.AgentName;
                        existingMachine.Frozen = newMachine.Frozen;
                        existingMachine.DataStore = newMachine.DataStore;
                        existingMachine.Environment = newMachine.Environment;
                        existingMachine.Path = newMachine.Path;
                        existingMachine.StorageIdentifier = newMachine.StorageIdentifier;
                        existingMachine.ReportedNetworks = newMachine.ReportedNetworks;
                        existingMachine.NetworkAdapters = newMachine.NetworkAdapters;
                        existingMachine.Drives = newMachine.Drives;
                        existingMachine.CpuCount = newMachine.CpuCount;
                        existingMachine.StartupMemory = newMachine.StartupMemory;
                        existingMachine.MinimumMemory = newMachine.MinimumMemory;
                        existingMachine.StartupMemory = newMachine.StartupMemory;
                        existingMachine.Features = newMachine.Features;
                    });


                });
            }
        }

        protected async Task<Option<Project>> FindProject(Guid tenantId, string projectIdentifier)
        {
            var isGuid = Guid.TryParse(projectIdentifier, out var projectId);

            if (isGuid)
                return await StateStore.For<Project>().GetByIdAsync(projectId);

            return await StateStore.For<Project>()
                .GetBySpecAsync(new ProjectSpecs.GetByName(tenantId, projectIdentifier));
        }

        protected async Task<Project> FindRequiredProject(Guid tenantId, string projectIdentifier)
        {
            var foundProject = await FindProject(tenantId, projectIdentifier);

            if(foundProject.IsNone)
                throw new NotFoundException($"Project '{projectIdentifier}' not found.");

            return foundProject.IfNone(new Project());
        }

        private Catlet VirtualMachineInfoToCatlet(VirtualMachineData vmInfo, CatletFarm hostMachine,
            Guid machineId, Project project)
        {
            return new Catlet
            {
                Id = machineId,
                Project = project,
                ProjectId = project.Id,
                VMId = vmInfo.VMId,
                Name = vmInfo.Name,
                Status = MapVmStatusToMachineStatus(vmInfo.Status),
                Host = hostMachine,
                AgentName = hostMachine.Name,
                DataStore = vmInfo.DataStore,
                Environment = vmInfo.Environment,
                Path = vmInfo.VMPath,
                Frozen = vmInfo.Frozen,
                StorageIdentifier = vmInfo.StorageIdentifier,
                MetadataId = vmInfo.MetadataId,
                UpTime = vmInfo.UpTime,
                CpuCount = vmInfo.Cpu?.Count ?? 0,
                StartupMemory = vmInfo.Memory?.Startup ?? 0,
                MinimumMemory = vmInfo.Memory?.Minimum ?? 0,
                MaximumMemory = vmInfo.Memory?.Startup ?? 0,
                Features = MapFeatures(vmInfo),
                NetworkAdapters = vmInfo.NetworkAdapters.Select(a => new CatletNetworkAdapter
                {
                    Id = a.Id,
                    CatletId = machineId,
                    Name = a.AdapterName,
                    SwitchName = a.VirtualSwitchName
                }).ToList(),
                Drives = vmInfo.Drives.Select(d => new CatletDrive
                {
                    Id = d.Id,
                    CatletId = machineId,
                    Type = d.Type,
                    //TODO: this code may needs to be improved
                    AttachedDisk = d.Disk != null
                        ? _vhdDataService.GetVHD(d.Disk.Id).GetAwaiter().GetResult().IfNoneUnsafe(() => null) : null

                }).ToList(),
                ReportedNetworks = (vmInfo.Networks?.ToReportedNetwork(machineId) ?? Array.Empty<ReportedNetwork>()).ToList()
            };
        }

        private static List<CatletFeature> MapFeatures(VirtualMachineData vmInfo)
        {
            var features = new List<CatletFeature>();
            
            if(vmInfo.Firmware?.SecureBoot ?? false)
                features.Add(CatletFeature.SecureBoot);
            if (vmInfo.Cpu?.ExposeVirtualizationExtensions ?? false)
                features.Add(CatletFeature.NestedVirtualization);


            return features;
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

    }
}