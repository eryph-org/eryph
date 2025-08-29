using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Disks;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Rebus;
using Eryph.Resources.Disks;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Rebus.Pipeline;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Inventory
{
    internal class UpdateInventoryCommandHandlerBase
    {
        private readonly IOperationDispatcher _dispatcher;
        private readonly IVirtualMachineDataService _vmDataService;
        private readonly IInventoryLockManager _lockManager;
        private readonly IVirtualMachineMetadataService _metadataService;
        private readonly IStateStore _stateStore;
        private readonly IMessageContext _messageContext;
        private readonly ILogger _logger;

        protected UpdateInventoryCommandHandlerBase(
            IInventoryLockManager lockManager,
            IVirtualMachineMetadataService metadataService,
            IOperationDispatcher dispatcher,
            IVirtualMachineDataService vmDataService, 
            IStateStore stateStore,
            IMessageContext messageContext,
            ILogger logger)
        {
            _lockManager = lockManager;
            _metadataService = metadataService;
            _dispatcher = dispatcher;
            _vmDataService = vmDataService;
            _stateStore = stateStore;
            _messageContext = messageContext;
            _logger = logger;
        }

        protected async Task UpdateVMs(
            DateTimeOffset timestamp,
            IEnumerable<VirtualMachineData> vmList,
            CatletFarm hostMachine)
        {
            var vms = vmList as VirtualMachineData[] ?? vmList.ToArray();
            var diskInfos = vms.SelectMany(x => x.Drives)
                .Select(d => d.Disk)
                .Where(d => d != null)
                .ToSeq();

            // Acquire all necessary locks in the beginning to minimize the potential for deadlocks.
            foreach (var vhdId in CollectDiskIdentifiers(diskInfos))
            {
                await _lockManager.AcquireVhdLock(vhdId);
            }

            foreach (var vmId in vms.Select(x => x.VmId).OrderBy(g => g))
            {
                await _lockManager.AcquireVmLock(vmId);
            }

            foreach (var vmInfo in vms)
            {
                //get known metadata for VM, if metadata is unknown skip this VM as it is not in Eryph management
                var metadata = await _metadataService.GetMetadata(vmInfo.MetadataId);
                if (metadata is null)
                {
                    _logger.LogTrace("Skipping VM {VmId} during inventory as it is not managed by eryph...", vmInfo.VmId);
                    continue;
                }

                var project = await FindRequiredProject(vmInfo.ProjectName, vmInfo.ProjectId);
                if (project.BeingDeleted)
                {
                    _logger.LogDebug("Skipping inventory update for VM {VmId}. The project {ProjectName}({ProjectId}) is marked as deleted.",
                        vmInfo.VmId, project.Name, project.Id);
                    return;
                }

                var optionalMachine = await _vmDataService.Get(metadata.CatletId);

                //machine not found or metadata is assigned to new VM - a new VM resource will be created
                if (optionalMachine.IsNone || metadata.VmId != vmInfo.VmId)
                {
                    var catletId = metadata.CatletId;
                    var metadataId = metadata.Id;
                    
                    if (metadata.VmId != vmInfo.VmId)
                    {
                        // This VM is a copy/import of another VM. We assign
                        // new IDs and track it as separate catlet.
                        catletId = Guid.NewGuid();
                        metadataId = Guid.NewGuid();

                        await _metadataService.AddMetadata(
                            new CatletMetadata
                            {
                                Id = metadataId,
                                CatletId = catletId,
                                VmId = vmInfo.VmId,
                                Metadata = metadata.Metadata,
                                IsDeprecated = metadata.IsDeprecated,
                                // TODO Should we set this to false to force a sanitization
                                SecretDataHidden = metadata.SecretDataHidden,
                            });


                        await _dispatcher.StartNew(
                            project.TenantId,
                            _messageContext.GetTraceId(),
                            new UpdateCatletMetadataCommand
                            {
                                AgentName = hostMachine.Name,
                                CurrentMetadataId = metadata.Id,
                                NewMetadataId = metadataId,
                                CatletId = catletId,
                                VmId = vmInfo.VmId,
                            });
                    }

                    var catlet = await VirtualMachineInfoToCatlet(
                        vmInfo, hostMachine, timestamp, catletId, project);
                    catlet.MetadataId = metadataId;
                    catlet.IsDeprecated = metadata.IsDeprecated;
                    await _vmDataService.Add(catlet);

                    return;
                }

                await optionalMachine.IfSomeAsync(async existingMachine =>
                {
                    if (existingMachine.LastSeen >= timestamp)
                    {
                        _logger.LogDebug("Skipping inventory update for catlet {CatletId} with timestamp {Timestamp:O}. Most recent information is dated {LastSeen:O}.",
                            existingMachine.Id, timestamp, existingMachine.LastSeen);
                        return;
                    }
                    
                    existingMachine.LastSeen = timestamp;

                    await _stateStore.LoadPropertyAsync(existingMachine, x => x.Project);

                    Debug.Assert(existingMachine.Project != null);  

                    await _stateStore.LoadCollectionAsync(existingMachine, x => x.ReportedNetworks);
                    await _stateStore.LoadCollectionAsync(existingMachine, x => x.NetworkAdapters);


                    // update data for existing machine
                    var newMachine = await VirtualMachineInfoToCatlet(vmInfo,
                        hostMachine, timestamp, existingMachine.Id, existingMachine.Project);
                    existingMachine.Name = newMachine.Name;
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
                    existingMachine.MaximumMemory = newMachine.MaximumMemory;
                    existingMachine.Features = newMachine.Features;
                    existingMachine.SecureBootTemplate = newMachine.SecureBootTemplate;

                    if (existingMachine.LastSeenState >= timestamp)
                    {
                        _logger.LogDebug("Skipping state update for catlet {CatletId} with timestamp {Timestamp:O}. Most recent state information is dated {LastSeen:O}.",
                            existingMachine.Id, timestamp, existingMachine.LastSeenState);
                        return;
                    }

                    existingMachine.LastSeenState = timestamp;
                    existingMachine.Status = newMachine.Status;
                    existingMachine.UpTime = newMachine.UpTime;
                });
            }
        }

        protected async Task<Option<Project>> FindProject(
            string projectName, Guid? optionalProjectId)
        {
            if (optionalProjectId.GetValueOrDefault() != Guid.Empty)
                return await _stateStore.For<Project>().GetByIdAsync(optionalProjectId.GetValueOrDefault());

            if (string.IsNullOrWhiteSpace(projectName))
                projectName = EryphConstants.DefaultProjectName;

            return await _stateStore.For<Project>()
                .GetBySpecAsync(new ProjectSpecs.GetByName(
                    EryphConstants.DefaultTenantId, projectName));
        }

        protected async Task<Project> FindRequiredProject(string projectName,
            Guid? projectId)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                projectName = EryphConstants.DefaultProjectName;

            var foundProject = await FindProject(projectName, projectId);

            return foundProject.IfNone(
                () => throw new NotFoundException(
                    $"Project '{(projectId.HasValue ? projectId : projectName)}' not found."));
        }

        private Task<Catlet> VirtualMachineInfoToCatlet(
            VirtualMachineData vmInfo,
            CatletFarm hostMachine,
            DateTimeOffset timestamp,
            Guid machineId,
            Project project) =>
            from _ in Task.FromResult(unit)
            from drives in vmInfo.Drives.ToSeq()
                .Map(d => VirtualMachineDriveDataToCatletDrive(d, hostMachine.Name, timestamp))
                .SequenceSerial()
            select new Catlet
            {
                Id = machineId,
                Project = project,
                ProjectId = project.Id,
                VmId = vmInfo.VmId,
                Name = vmInfo.Name,
                Status = vmInfo.Status.ToCatletStatus(),
                Host = hostMachine,
                AgentName = hostMachine.Name,
                DataStore = vmInfo.DataStore,
                Environment = vmInfo.Environment,
                Path = vmInfo.VMPath,
                Frozen = vmInfo.Frozen,
                StorageIdentifier = vmInfo.StorageIdentifier,
                MetadataId = vmInfo.MetadataId,
                UpTime = vmInfo.Status is VmStatus.Stopped ? TimeSpan.Zero : vmInfo.UpTime,
                CpuCount = vmInfo.Cpu?.Count ?? 0,
                StartupMemory = vmInfo.Memory?.Startup ?? 0,
                MinimumMemory = vmInfo.Memory?.Minimum ?? 0,
                MaximumMemory = vmInfo.Memory?.Maximum ?? 0,
                Features = MapFeatures(vmInfo),
                SecureBootTemplate = vmInfo.Firmware?.SecureBootTemplate,
                NetworkAdapters = vmInfo.NetworkAdapters.Select(a => new CatletNetworkAdapter
                {
                    Id = a.Id,
                    CatletId = machineId,
                    Name = a.AdapterName,
                    SwitchName = a.VirtualSwitchName,
                    MacAddress = a.MacAddress,
                }).ToList(),
                Drives = drives.ToList(),
                ReportedNetworks = (vmInfo.Networks?.ToReportedNetwork(machineId) ?? []).ToList()
            };

        private async Task<CatletDrive> VirtualMachineDriveDataToCatletDrive(
            VirtualMachineDriveData driveData,
            string agentName,
            DateTimeOffset timestamp)
        {
            var disk = await Optional(driveData.Disk)
                .BindAsync(d => AddOrUpdateDisk(agentName, timestamp, d).ToAsync())
                .ToOption();

            return new CatletDrive
            {
                Id = driveData.Id,
                Type = driveData.Type ?? CatletDriveType.VHD,
                AttachedDisk = disk.IfNoneUnsafe(() => null)
            };
        }

        private static ISet<CatletFeature> MapFeatures(VirtualMachineData vmInfo)
        {
            var features = new System.Collections.Generic.HashSet<CatletFeature>();
            
            if (vmInfo.Firmware?.SecureBoot ?? false)
                features.Add(CatletFeature.SecureBoot);
            
            if (vmInfo.Cpu?.ExposeVirtualizationExtensions ?? false)
                features.Add(CatletFeature.NestedVirtualization);
            
            if (vmInfo.Memory?.DynamicMemoryEnabled ?? false)
                features.Add(CatletFeature.DynamicMemory);

            if (vmInfo.Security?.TpmEnabled ?? false)
                features.Add(CatletFeature.Tpm);
            
            return features;
        }

        protected async Task<Option<VirtualDisk>> AddOrUpdateDisk(
            string agentName,
            DateTimeOffset timestamp,
            DiskInfo diskInfo)
        {
            var disk = await GetDisk(agentName, diskInfo);
            if (disk is not null && (disk.LastSeen >= timestamp || disk.Project.BeingDeleted))
                return disk;

            Option<VirtualDisk> parentDisk = null;
            if (diskInfo.Parent is not null)
            {
                parentDisk = await AddOrUpdateDisk(agentName, timestamp, diskInfo.Parent);
            }

            if (disk is not null)
            {
                // We do not attempt to update the project of an existing disks.
                // Disks are looked up per project so we are always creating a
                // new disk entry in the database.

                disk.Parent = parentDisk.IfNoneUnsafe(() => null);
                disk.ParentPath = diskInfo.ParentPath;
                disk.SizeBytes = diskInfo.SizeBytes;
                disk.UsedSizeBytes = diskInfo.UsedSizeBytes;
                disk.Frozen = diskInfo.Frozen;
                disk.Deleted = false;
                disk.LastSeen = timestamp;
                disk.LastSeenAgent = agentName;
                disk.Status = diskInfo.Status.ToVirtualDiskStatus();
                await _stateStore.SaveChangesAsync();
                return disk;
            }

            var project = await FindProject(diskInfo.ProjectName, diskInfo.ProjectId)
                .IfNoneAsync(() => FindRequiredProject(EryphConstants.DefaultProjectName, null));
            if (project.BeingDeleted)
                return None;

            disk = new VirtualDisk
            {
                Id = diskInfo.Id,
                Name = diskInfo.Name,
                DiskIdentifier = diskInfo.DiskIdentifier,
                DataStore = diskInfo.DataStore,
                Environment = diskInfo.Environment,
                StorageIdentifier = diskInfo.StorageIdentifier,
                Project = project,
                FileName = diskInfo.FileName,
                Path = diskInfo.Path.ToLowerInvariant(),
                GeneSet = diskInfo.Gene?.Id.GeneSet.Value,
                GeneName = diskInfo.Gene?.Id.GeneName.Value,
                GeneArchitecture = diskInfo.Gene?.Architecture.Value,
                SizeBytes = diskInfo.SizeBytes,
                UsedSizeBytes = diskInfo.UsedSizeBytes,
                Frozen = diskInfo.Frozen,
                LastSeen = timestamp,
                LastSeenAgent = agentName,
                Parent = parentDisk.IfNoneUnsafe(() => null),
                ParentPath = diskInfo.ParentPath,
                Status = diskInfo.Status.ToVirtualDiskStatus(),
            };
            await _stateStore.For<VirtualDisk>().AddAsync(disk);
            await _stateStore.SaveChangesAsync();
            return disk;
        }

        protected async Task CheckDisks(
            DateTimeOffset timestamp,
            string agentName)
        {
            var outdatedDisks = await _stateStore.For<VirtualDisk>().ListAsync(
                new VirtualDiskSpecs.FindOutdated(timestamp, agentName));
            if (outdatedDisks.Count == 0)
                return;

            await _dispatcher.StartNew(
                EryphConstants.DefaultTenantId,
                Guid.NewGuid().ToString(),
                new CheckDisksExistsCommand
                {
                    AgentName = agentName,
                    Disks = outdatedDisks.Select(d => new DiskInfo
                    {
                        Id = d.Id,
                        ProjectId = d.Project.Id,
                        ProjectName = d.Project.Name,
                        DataStore = d.DataStore,
                        Environment = d.Environment,
                        StorageIdentifier = d.StorageIdentifier,
                        Name = d.Name,
                        FileName = d.FileName,
                        Path = d.Path,
                        DiskIdentifier = d.DiskIdentifier,
                        Gene = d.ToUniqueGeneId(GeneType.Volume)
                            .IfNoneUnsafe((UniqueGeneIdentifier?)null),
                    }).ToArray()
                });
        }

        protected async Task<VirtualDisk?> GetDisk(
            string agentName, DiskInfo diskInfo)
        {
            var project = await FindProject(diskInfo.ProjectName, diskInfo.ProjectId)
                .IfNoneAsync(() => FindRequiredProject(EryphConstants.DefaultProjectName, null));

            var virtualDisks = await _stateStore.For<VirtualDisk>().ListAsync(
                new VirtualDiskSpecs.GetByLocation(
                    project.Id,
                    diskInfo.DataStore,
                    diskInfo.Environment,
                    diskInfo.StorageIdentifier,
                    diskInfo.Name,
                    diskInfo.DiskIdentifier));

            return virtualDisks.Length() > 1
                ? virtualDisks.FirstOrDefault(d => 
                    string.Equals(d.Path, diskInfo.Path, StringComparison.OrdinalIgnoreCase) 
                    && string.Equals(d.FileName, diskInfo.FileName, StringComparison.OrdinalIgnoreCase))
                : virtualDisks.FirstOrDefault();
        }

        protected Seq<Guid> CollectDiskIdentifiers(Seq<DiskInfo> diskInfos) =>
            diskInfos.Map(d => Optional(d.Parent)).Somes()
                .Match(Empty: Seq<Guid>, Seq: CollectDiskIdentifiers)
                .Append(diskInfos.Map(d => d.DiskIdentifier))
                .Distinct()
                .Order()
                .ToSeq();

        protected bool IsUpdateOutdated(CatletFarm vmHost, DateTimeOffset timestamp)
        {
            if (vmHost.LastInventory >= timestamp)
            {
                _logger.LogInformation(
                    "Skipping inventory update for host {Hostname} with timestamp {Timestamp:O}. Most recent information is dated {LastInventory:O}.",
                    vmHost.Name, timestamp, vmHost.LastInventory);
                return true;
            }
            return false;
        }
    }
}
