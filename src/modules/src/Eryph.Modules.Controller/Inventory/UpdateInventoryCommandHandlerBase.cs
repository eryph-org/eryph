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

        protected async Task UpdateVMs(
            DateTimeOffset timestamp,
            IEnumerable<VirtualMachineData> vmList, CatletFarm hostMachine)
        {

            var vms = vmList as VirtualMachineData[] ?? vmList.ToArray();

            var diskInfos = vms.SelectMany(x => x.Drives.Select(d => d.Disk)).ToList();
            var allDisks = new List<DiskInfo>();
            foreach (var diskInfo in diskInfos) SelectAllParentDisks(ref allDisks, diskInfo);

            diskInfos = allDisks.Distinct((x, y) =>
                string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.FileName, y.FileName, StringComparison.OrdinalIgnoreCase)).ToList();

            var addedDisks = new List<VirtualDisk>();

            foreach (var diskInfo in diskInfos)
            {
                var project = await FindProject(diskInfo.ProjectName, diskInfo.ProjectId)
                    .IfNoneAsync(() =>
                        FindRequiredProject("default", Guid.Empty));

                var disk = await LookupVirtualDisk(diskInfo, project)
                    .IfNoneAsync(async () =>
                {
                    
                    var d = new VirtualDisk
                    {
                        Id = diskInfo.Id,
                        Name = diskInfo.Name,
                        DiskIdentifier = diskInfo.DiskIdentifier,
                        DataStore = diskInfo.DataStore,
                        Environment = diskInfo.Environment,
                        Geneset = diskInfo.Geneset,
                        StorageIdentifier = diskInfo.StorageIdentifier,
                        Project = project,
                        FileName = diskInfo.FileName,
                        Path = diskInfo.Path.ToLowerInvariant()

                    };
                    d = await _vhdDataService.AddNewVHD(d);
                    addedDisks.Add(d);
                    return d;
                });

                disk.SizeBytes = diskInfo.SizeBytes;
                disk.UsedSizeBytes = diskInfo.UsedSizeBytes;
                disk.Frozen = diskInfo.Frozen;
                disk.LastSeen = timestamp;
                disk.LastSeenAgent = hostMachine.Name;
                await _vhdDataService.UpdateVhd(disk);

            }

            //second loop to assign parents and to update state db
            foreach (var diskInfo in diskInfos)
            {
                var project = await FindProject(diskInfo.ProjectName, diskInfo.ProjectId)
                    .IfNoneAsync(() =>
                        FindRequiredProject("default", Guid.Empty));

                await LookupVirtualDisk(diskInfo, project).IfSomeAsync(async currentDisk =>
               {
                   if (diskInfo.Parent == null)
                   {
                       currentDisk.Parent = null;
                       return;
                   }

                   await LookupVirtualDisk(diskInfo.Parent, project)
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

                await optionalMetadata.IfSomeAsync(async metadata =>
                {
                    var optionalMachine = (await _vmDataService.GetVM(metadata.MachineId));
                    var project = await FindRequiredProject(vmInfo.ProjectName, vmInfo.ProjectId);

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
                                project.TenantId,
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
                        existingMachine.SecureBootTemplate = newMachine.SecureBootTemplate;
                    });


                });
            }

            return;

            async Task<Option<VirtualDisk>> LookupVirtualDisk(DiskInfo diskInfo, Project project)
            {
                    
                return await _vhdDataService.FindVHDByLocation(
                        project.Id,
                        diskInfo.DataStore,
                        diskInfo.Environment,
                        diskInfo.StorageIdentifier,
                        diskInfo.Name,
                        diskInfo.DiskIdentifier)
                    .Map(l => addedDisks.Append(l))
                    .Map(l => l.Filter(
                        x => x.DataStore == diskInfo.DataStore &&
                             x.Project.Name == diskInfo.ProjectName &&
                             x.Environment == diskInfo.Environment &&
                             x.StorageIdentifier == diskInfo.StorageIdentifier &&
                             x.Name == diskInfo.Name))
                    .Map(x => x.ToArray())
                    .Map(candidates => candidates.Length <= 1
                    ? candidates.HeadOrNone()
                    : candidates.Find(x =>
                        string.Equals(x.Path, diskInfo.Path, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.FileName, diskInfo.FileName, StringComparison.OrdinalIgnoreCase)));
            }
        }

        protected async Task<Option<Project>> FindProject(
            string projectName, Guid? optionalProjectId)
        {
            if (optionalProjectId.GetValueOrDefault() != default)
                return await StateStore.For<Project>().GetByIdAsync(optionalProjectId.GetValueOrDefault());

            if(string.IsNullOrWhiteSpace(projectName))
                projectName = "default";

            return await StateStore.For<Project>()
                .GetBySpecAsync(new ProjectSpecs.GetByName(
                    EryphConstants.DefaultTenantId, projectName));
        }

        protected async Task<Project> FindRequiredProject(string projectName,
            Guid? projectId)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                projectName = "default";

            var foundProject = await FindProject(projectName, projectId);

            if(foundProject.IsNone && !projectId.HasValue)
                throw new NotFoundException($"Project '{projectName}' not found.");

            if (foundProject.IsNone && projectId.HasValue)
                throw new NotFoundException($"Project '{projectId}' not found.");

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
                SecureBootTemplate = vmInfo.Firmware?.SecureBootTemplate,
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