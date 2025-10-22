using System;
using System.Collections.Generic;
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
using Microsoft.Extensions.Logging;
using Rebus.Pipeline;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Controller.Inventory
{
    internal class UpdateInventoryCommandHandlerBase
    {
        private readonly IOperationDispatcher _dispatcher;
        private readonly ICatletDataService _vmDataService;
        private readonly IInventoryLockManager _lockManager;
        private readonly ICatletMetadataService _metadataService;
        private readonly IStateStore _stateStore;
        private readonly IMessageContext _messageContext;
        private readonly ILogger _logger;

        protected UpdateInventoryCommandHandlerBase(
            IInventoryLockManager lockManager,
            ICatletMetadataService metadataService,
            IOperationDispatcher dispatcher,
            ICatletDataService vmDataService, 
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

        protected async Task UpdateVms(
            DateTimeOffset timestamp,
            IReadOnlyList<VirtualMachineData> vmInfos,
            CatletFarm host)
        {
            var diskInfos = vmInfos.SelectMany(x => x.Drives)
                .Select(d => d.Disk)
                .Where(d => d != null)
                .ToSeq();

            // Acquire all necessary locks in the beginning to minimize the potential for deadlocks.
            foreach (var vhdId in CollectDiskIdentifiers(diskInfos))
            {
                await _lockManager.AcquireVhdLock(vhdId);
            }

            foreach (var vmId in vmInfos.Select(x => x.VmId).OrderBy(g => g))
            {
                await _lockManager.AcquireVmLock(vmId);
            }

            foreach (var vmInfo in vmInfos)
            {
                await UpdateVm(timestamp, vmInfo, host);
            }
        }

        protected async Task UpdateVm(
            DateTimeOffset timestamp,
            VirtualMachineData vmInfo,
            CatletFarm host)
        {
            // Get known metadata for VM, if metadata is unknown skip this VM as it is not managed by eryph
            var metadata = await _metadataService.GetMetadata(vmInfo.MetadataId);
            if (metadata is null)
            {
                _logger.LogTrace("Skipping VM {VmId} during inventory as it is not managed by eryph...", vmInfo.VmId);
                return;
            }

            var project = await FindRequiredProject(vmInfo.ProjectName, vmInfo.ProjectId);
            if (project.BeingDeleted)
            {
                _logger.LogDebug("Skipping inventory update for VM {VmId}. The project {ProjectName}({ProjectId}) is marked as deleted.",
                    vmInfo.VmId, project.Name, project.Id);
                return;
            }

            if (metadata.VmId != vmInfo.VmId)
            {
                await AddCopiedVm(timestamp, vmInfo, host, project, metadata);
                return;
            }

            var existingCatlet = await _vmDataService.Get(metadata.CatletId);
            if (existingCatlet is null)
            {
                await AddNewVm(timestamp, vmInfo, host, project, metadata);
                return;
            }

            await UpdateExistingVm(timestamp, vmInfo, host, existingCatlet);
        }

        private async Task AddCopiedVm(
            DateTimeOffset timestamp,
            VirtualMachineData vmInfo,
            CatletFarm host,
            Project project,
            CatletMetadata existingMetadata)
        {
            // This VM is a copy/import of another VM. We assign
            // new IDs and track it as a separate catlet.
            var catletId = Guid.NewGuid();
            var metadataId = Guid.NewGuid();

            await _metadataService.AddMetadata(new CatletMetadata
            {
                Id = metadataId,
                CatletId = catletId,
                VmId = vmInfo.VmId,
                Metadata = existingMetadata.Metadata,
                IsDeprecated = existingMetadata.IsDeprecated,
                SecretDataHidden = existingMetadata.SecretDataHidden,
                // We intentionally do not copy the specification information as a copied VM
                // should no longer be associated with the catlets specification.
            });


            await _dispatcher.StartNew(
                project.TenantId,
                _messageContext.GetTraceId(),
                new UpdateCatletMetadataCommand
                {
                    AgentName = host.Name,
                    CurrentMetadataId = existingMetadata.Id,
                    NewMetadataId = metadataId,
                    CatletId = catletId,
                    VmId = vmInfo.VmId,
                });
            

            var newCatlet = await VirtualMachineInfoToCatlet(
                vmInfo, host, timestamp, catletId, project);
            newCatlet.MetadataId = metadataId;
            newCatlet.IsDeprecated = existingMetadata.IsDeprecated;
            
            await _vmDataService.Add(newCatlet);
        }

        private async Task AddNewVm(
            DateTimeOffset timestamp,
            VirtualMachineData vmInfo,
            CatletFarm host,
            Project project,
            CatletMetadata existingMetadata)
        {
            var newCatlet = await VirtualMachineInfoToCatlet(
                vmInfo, host, timestamp, existingMetadata.CatletId, project);
            newCatlet.MetadataId = existingMetadata.Id;
            newCatlet.IsDeprecated = existingMetadata.IsDeprecated;
            newCatlet.SpecificationId = existingMetadata.SpecificationId;
            newCatlet.SpecificationVersionId = existingMetadata.SpecificationVersionId;

            await _vmDataService.Add(newCatlet);
        }

        private async Task UpdateExistingVm(
            DateTimeOffset timestamp,
            VirtualMachineData vmInfo,
            CatletFarm host,
            Catlet existingCatlet)
        {
            // Skip the update when we already have newer data
            if (existingCatlet.LastSeen >= timestamp)
            {
                _logger.LogDebug("Skipping inventory update for catlet {CatletId} with timestamp {Timestamp:O}. Most recent information is dated {LastSeen:O}.",
                    existingCatlet.Id, timestamp, existingCatlet.LastSeen);
                return;
            }

            await _stateStore.LoadCollectionAsync(existingCatlet, x => x.ReportedNetworks);
            await _stateStore.LoadCollectionAsync(existingCatlet, x => x.NetworkAdapters);

            var convertedVmInfo = await VirtualMachineInfoToCatlet(
                vmInfo, host, timestamp, existingCatlet.Id, existingCatlet.Project);

            existingCatlet.LastSeen = timestamp;
            existingCatlet.Name = convertedVmInfo.Name;
            existingCatlet.Host = host;
            existingCatlet.AgentName = convertedVmInfo.AgentName;
            existingCatlet.Frozen = convertedVmInfo.Frozen;
            existingCatlet.DataStore = convertedVmInfo.DataStore;
            existingCatlet.Environment = convertedVmInfo.Environment;
            existingCatlet.Path = convertedVmInfo.Path;
            existingCatlet.StorageIdentifier = convertedVmInfo.StorageIdentifier;
            existingCatlet.ReportedNetworks = convertedVmInfo.ReportedNetworks;
            existingCatlet.NetworkAdapters = convertedVmInfo.NetworkAdapters;
            existingCatlet.Drives = convertedVmInfo.Drives;
            existingCatlet.CpuCount = convertedVmInfo.CpuCount;
            existingCatlet.StartupMemory = convertedVmInfo.StartupMemory;
            existingCatlet.MinimumMemory = convertedVmInfo.MinimumMemory;
            existingCatlet.MaximumMemory = convertedVmInfo.MaximumMemory;
            existingCatlet.Features = convertedVmInfo.Features;
            existingCatlet.SecureBootTemplate = convertedVmInfo.SecureBootTemplate;

            // Skip the update of the state information when we already have newer data.
            // We must check this separately as the state information is monitored separately.
            if (existingCatlet.LastSeenState >= timestamp)
            {
                _logger.LogDebug("Skipping state update for catlet {CatletId} with timestamp {Timestamp:O}. Most recent state information is dated {LastSeen:O}.",
                    existingCatlet.Id, timestamp, existingCatlet.LastSeenState);
                return;
            }

            existingCatlet.LastSeenState = timestamp;
            existingCatlet.Status = convertedVmInfo.Status;
            existingCatlet.UpTime = convertedVmInfo.UpTime;
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
            Guid catletId,
            Project project) =>
            from _ in Task.FromResult(unit)
            from drives in vmInfo.Drives.ToSeq()
                .Map(d => VirtualMachineDriveDataToCatletDrive(d, hostMachine.Name, timestamp))
                .SequenceSerial()
            select new Catlet
            {
                Id = catletId,
                Project = project,
                ProjectId = project.Id,
                VmId = vmInfo.VmId,
                Name = vmInfo.Name,
                Status = vmInfo.Status.ToCatletStatus(),
                LastSeen = timestamp,
                LastSeenState = timestamp,
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
                    CatletId = catletId,
                    Name = a.AdapterName,
                    SwitchName = a.VirtualSwitchName,
                    MacAddress = a.MacAddress,
                }).ToList(),
                Drives = drives.ToList(),
                ReportedNetworks = (vmInfo.Networks?.ToReportedNetwork(catletId) ?? []).ToList()
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
                Type = driveData.Type ?? CatletDriveType.Dvd,
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
