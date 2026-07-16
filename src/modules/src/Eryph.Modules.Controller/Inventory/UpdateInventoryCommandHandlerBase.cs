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

namespace Eryph.Modules.Controller.Inventory;

internal class UpdateInventoryCommandHandlerBase
{
    private readonly IOperationDispatcher _dispatcher;
    private readonly IInventoryLockManager _lockManager;
    private readonly ILogger _logger;
    private readonly IMessageContext _messageContext;
    private readonly ICatletMetadataService _metadataService;
    private readonly IStateStore _stateStore;
    private readonly ICatletDataService _vmDataService;

    /// <summary>
    /// The catlet names taken during this message, which the database cannot be asked about until the
    /// unit of work commits.
    /// </summary>
    private readonly System.Collections.Generic.HashSet<(Guid ProjectId, string Environment, string Name)>
        _namesTaken = [];

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
        var diskInfos = vmInfos.SelectMany(x => x.Drives ?? [])
            .Select(d => d.Disk)
            .OfType<DiskInfo>()
            .ToSeq();

        // Acquire all necessary locks in the beginning to minimize the potential for deadlocks.
        foreach (var vhdId in CollectDiskIdentifiers(diskInfos)) await _lockManager.AcquireVhdLock(vhdId);

        foreach (var vmId in vmInfos.Select(x => x.VmId).OrderBy(g => g)) await _lockManager.AcquireVmLock(vmId);

        // Scoped to this batch: it stands in for what the database cannot be asked about until the
        // unit of work commits, and that unit of work is exactly this message.
        _namesTaken.Clear();

        foreach (var vmInfo in vmInfos) await UpdateVm(timestamp, vmInfo, host);
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
            _logger.LogDebug(
                "Skipping inventory update for VM {VmId}. The project {ProjectName}({ProjectId}) is marked as deleted.",
                vmInfo.VmId, project.Name, project.Id);
            return;
        }

        var existingCatlet = metadata.VmId == vmInfo.VmId
            ? await _vmDataService.Get(metadata.CatletId)
            : null;

        if (existingCatlet is not null)
        {
            await UpdateExistingVm(timestamp, vmInfo, host, existingCatlet);
            return;
        }

        // A catlet is created here, so its environment must be derived from where the VM was found.
        // Nothing else knows it: unlike an update there is no stored value to fall back on. Skip the
        // VM when the path cannot be attributed rather than inventing an environment — the
        // environment is part of a catlet's identity, and a wrong one is worse than a missing catlet.
        if (vmInfo.Environment is null)
        {
            _logger.LogWarning(
                "Skipping VM {VmId} during inventory: its storage location could not be attributed to "
                + "an environment. Check that the datastore paths of this host match the storage "
                + "configuration.", vmInfo.VmId);
            return;
        }

        // A catlet is identified by its name within a project and environment, and this one is being
        // recorded for the first time. A VM copied on the host keeps its name, so it collides with
        // the catlet it was copied from. The insert would only fail when the unit of work commits,
        // taking the whole host's inventory round with it and repeating on every retry — one
        // unmanaged VM would freeze the inventory of every catlet on that host. Skip it instead.
        if (!await TryTakeName(
                project.Id, vmInfo.Environment, NormalizeName(vmInfo.Name), excludedCatletId: null))
        {
            _logger.LogWarning(
                "Skipping VM {VmId} during inventory: a catlet named '{Name}' already exists in the "
                + "environment '{Environment}' of project {ProjectName}. Rename the VM to have it "
                + "inventoried as a separate catlet.",
                vmInfo.VmId, vmInfo.Name, vmInfo.Environment, project.Name);
            return;
        }

        if (metadata.VmId != vmInfo.VmId)
        {
            await AddCopiedVm(timestamp, vmInfo, host, project, metadata);
            return;
        }

        await AddNewVm(timestamp, vmInfo, host, project, metadata);
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
            // should no longer be associated with the original catlet's specification.
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
            vmInfo, host, timestamp, catletId, project, vmInfo.Environment!, host.SiteId);
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
            vmInfo, host, timestamp, existingMetadata.CatletId, project,
            vmInfo.Environment!, host.SiteId);
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
            _logger.LogDebug(
                "Skipping inventory update for catlet {CatletId} with timestamp {Timestamp:O}. Most recent information is dated {LastSeen:O}.",
                existingCatlet.Id, timestamp, existingCatlet.LastSeen);
            return;
        }

        await _stateStore.LoadCollectionAsync(existingCatlet, x => x.ReportedNetworks);
        await _stateStore.LoadCollectionAsync(existingCatlet, x => x.NetworkAdapters);

        // The catlet exists, so its environment and site are already decided: pass them back in rather
        // than deriving them from the VM's path, which is only a second observation of the same fact.
        var convertedVmInfo = await VirtualMachineInfoToCatlet(
            vmInfo, host, timestamp, existingCatlet.Id, existingCatlet.Project,
            existingCatlet.Environment, existingCatlet.SiteId);

        WarnAboutDivergedLocation(existingCatlet, vmInfo, host);

        existingCatlet.LastSeen = timestamp;

        // A rename on the host can collide with a sibling catlet, which would fail the whole
        // inventory round when the unit of work commits. Keep the stored name in that case: the
        // catlet stays inventoried under the name it can actually have.
        if (!string.Equals(convertedVmInfo.Name, existingCatlet.Name, StringComparison.OrdinalIgnoreCase)
            && !await TryTakeName(
                existingCatlet.ProjectId, existingCatlet.Environment, convertedVmInfo.Name,
                excludedCatletId: existingCatlet.Id))
        {
            _logger.LogWarning(
                "Catlet {CatletId} was renamed to '{Name}' on host {HostName}, but a catlet with that "
                + "name already exists in its environment. It keeps the name '{StoredName}'.",
                existingCatlet.Id, convertedVmInfo.Name, host.Name, existingCatlet.Name);
        }
        else
        {
            existingCatlet.Name = convertedVmInfo.Name;
        }

        existingCatlet.Host = host;
        existingCatlet.AgentName = convertedVmInfo.AgentName;
        existingCatlet.Frozen = convertedVmInfo.Frozen;
        existingCatlet.DataStore = convertedVmInfo.DataStore;
        // The environment and the site are deliberately not updated. Both are decided when the catlet
        // is deployed and are immutable for its lifetime, so the value derived from the VM's path here
        // is a second observation of the same fact, not a newer one: when it agrees the write is a
        // no-op, and when it disagrees the stored value is the correct one. Writing it could only
        // destroy information — including the catlet's identity, of which the environment is part.
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

        // Provisioning status is observed and timestamped independently of the VM
        // state (the agent's provisioning monitor reports it out of band). Only
        // apply the inventory baseline when it was actually read and is newer.
        if (vmInfo.ProvisioningStatus is { } provisioningStatus
            && existingCatlet.LastSeenProvisioningStatus < timestamp)
        {
            existingCatlet.ProvisioningStatus = provisioningStatus.ToCatletProvisioningStatus();
            existingCatlet.LastSeenProvisioningStatus = timestamp;
        }

        // Skip the update of the state information when we already have newer data.
        // We must check this separately as the state information is monitored separately.
        if (existingCatlet.LastSeenState >= timestamp)
        {
            _logger.LogDebug(
                "Skipping state update for catlet {CatletId} with timestamp {Timestamp:O}. Most recent state information is dated {LastSeen:O}.",
                existingCatlet.Id, timestamp, existingCatlet.LastSeenState);
            return;
        }

        existingCatlet.LastSeenState = timestamp;
        existingCatlet.Status = convertedVmInfo.Status;
        existingCatlet.UpTime = convertedVmInfo.UpTime;
    }

    /// <summary>
    /// The name a catlet is stored under. Every lookup compares against the lower-cased name, so a
    /// name observed on the host is stored the same way — otherwise a VM named 'Web1' would be a
    /// separate catlet from 'web1' on a case-sensitive database and the same one on a
    /// case-insensitive database, and the uniqueness of a catlet's name would mean two different
    /// things depending on the provider. Names created by eryph are already lower-cased, so this
    /// only affects VMs named on the host.
    /// </summary>
    private static string? NormalizeName(string? name) => name?.ToLowerInvariant();

    /// <summary>
    /// Claims a catlet name within a project and environment, which is what a catlet is identified
    /// by, and reports whether the caller may write it. Every caller which is told the name is free
    /// goes on to write it, so claiming and checking are the same step. Checked before the write
    /// rather than caught afterwards: the unit of work commits once for the whole host, so a
    /// violation could not be attributed back to the VM which caused it and would fail the inventory
    /// of every catlet on that host.
    /// </summary>
    private async Task<bool> TryTakeName(
        Guid projectId, string environment, string? name, Guid? excludedCatletId)
    {
        if (name is null)
            return true;

        var catlet = await _stateStore.For<Catlet>().GetBySpecAsync(
            new CatletSpecs.GetByName(name, projectId, environment));

        if (catlet is not null)
            return catlet.Id == excludedCatletId;

        // The query only sees what is committed, and the unit of work covers the whole host: two
        // VMs first seen in the same round can carry the same name, and neither is in the database
        // yet. Without this they would both be written and the commit — not either VM — would fail,
        // which is the whole failure this check exists to prevent.
        return _namesTaken.Add((projectId, environment.ToLowerInvariant(), name));
    }

    /// <summary>
    /// Reports a catlet whose stored location disagrees with what the inventory observed. Neither is
    /// corrected: the stored environment and site are the authoritative ones, and silently rewriting
    /// either would change the catlet's identity or lie about where it runs. The divergence is real
    /// though, so it must not pass unnoticed — until now there was no signal for it at all.
    /// </summary>
    private void WarnAboutDivergedLocation(
        Catlet existingCatlet, VirtualMachineData vmInfo, CatletFarm host)
    {
        if (vmInfo.Environment is null)
            _logger.LogWarning(
                "The storage location of catlet {CatletId} could not be attributed to an environment; "
                + "it remains in the environment '{Environment}'. Check that the datastore paths of "
                + "host {HostName} match the storage configuration.",
                existingCatlet.Id, existingCatlet.Environment, host.Name);
        else if (!string.Equals(
                     vmInfo.Environment, existingCatlet.Environment, StringComparison.OrdinalIgnoreCase))
            _logger.LogWarning(
                "Catlet {CatletId} is in the environment '{Environment}' but its storage location is "
                + "attributed to the environment '{ObservedEnvironment}' on host {HostName}. The "
                + "environment of a catlet cannot change; its files may have been moved.",
                existingCatlet.Id, existingCatlet.Environment, vmInfo.Environment, host.Name);

        if (host.SiteId != existingCatlet.SiteId)
            _logger.LogWarning(
                "Catlet {CatletId} is pinned to a site but runs on host {HostName}, which is in a "
                + "different one. Moving a catlet between sites must be an explicit operation.",
                existingCatlet.Id, host.Name);
    }

    protected async Task<Option<Project>> FindProject(
        string? projectName, Guid? optionalProjectId)
    {
        if (optionalProjectId.GetValueOrDefault() != Guid.Empty)
            return await _stateStore.For<Project>().GetByIdAsync(optionalProjectId.GetValueOrDefault());

        if (string.IsNullOrWhiteSpace(projectName))
            projectName = EryphConstants.DefaultProjectName;

        return await _stateStore.For<Project>()
            .GetBySpecAsync(new ProjectSpecs.GetByName(
                EryphConstants.DefaultTenantId, projectName));
    }

    protected async Task<Project> FindRequiredProject(string? projectName,
        Guid? projectId)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            projectName = EryphConstants.DefaultProjectName;

        var foundProject = await FindProject(projectName, projectId);

        return foundProject.IfNone(() => throw new NotFoundException(
            $"Project '{(projectId.HasValue ? projectId : projectName)}' not found."));
    }

    private Task<Catlet> VirtualMachineInfoToCatlet(
        VirtualMachineData vmInfo,
        CatletFarm hostMachine,
        DateTimeOffset timestamp,
        Guid catletId,
        Project project,
        string environment,
        Guid siteId)
    {
        // A managed VM (it passed the metadata check) always reports these required
        // identity fields; treat a missing value as an inconsistent inventory.
        var name = NormalizeName(vmInfo.Name) ?? throw new InvalidOperationException(
            $"The inventory data for VM {vmInfo.VmId} is missing the name.");
        var dataStore = vmInfo.DataStore ?? throw new InvalidOperationException(
            $"The inventory data for VM {vmInfo.VmId} is missing the data store.");

        return
            from _ in Task.FromResult(unit)
            from drives in vmInfo.Drives.ToSeq()
                .Map(d => VirtualMachineDriveDataToCatletDrive(
                    d, hostMachine.Name, hostMachine.SiteId, timestamp))
                .SequenceSerial()
            select new Catlet
            {
                Id = catletId,
                Project = project,
                ProjectId = project.Id,
                SiteId = siteId,
                VmId = vmInfo.VmId,
                Name = name,
                Status = vmInfo.Status.ToCatletStatus(),
                LastSeen = timestamp,
                LastSeenState = timestamp,
                // Seed the provisioning status. When inventory could not read it
                // (VM not running / no guest-services) it stays Unknown and its
                // observation time stays at the Unix-epoch sentinel so a later report
                // wins. Must not be default(DateTimeOffset) (0001-01-01): that is below
                // MySQL datetime's minimum and would fail the insert on that provider.
                ProvisioningStatus = vmInfo.ProvisioningStatus?.ToCatletProvisioningStatus()
                                     ?? CatletProvisioningStatus.Unknown,
                LastSeenProvisioningStatus = vmInfo.ProvisioningStatus is not null
                    ? timestamp
                    : DateTimeOffset.UnixEpoch,
                Host = hostMachine,
                AgentName = hostMachine.Name,
                DataStore = dataStore,
                Environment = environment,
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
            NetworkAdapters = (vmInfo.NetworkAdapters ?? []).Select(a => new CatletNetworkAdapter
            {
                Id = a.Id ?? throw new InvalidOperationException(
                    $"The inventory data for VM {vmInfo.VmId} has a network adapter without an ID."),
                CatletId = catletId,
                Name = a.AdapterName ?? throw new InvalidOperationException(
                    $"The inventory data for VM {vmInfo.VmId} has a network adapter without a name."),
                SwitchName = a.VirtualSwitchName,
                MacAddress = a.MacAddress,
            }).ToList(),
            Drives = drives.ToList(),
            ReportedNetworks = (vmInfo.Networks?.ToReportedNetwork(catletId) ?? []).ToList(),
        };
    }

    private async Task<CatletDrive> VirtualMachineDriveDataToCatletDrive(
        VirtualMachineDriveData driveData,
        string agentName,
        Guid siteId,
        DateTimeOffset timestamp)
    {
        var disk = await Optional(driveData.Disk)
            .BindAsync(d => AddOrUpdateDisk(agentName, siteId, timestamp, d).ToAsync())
            .ToOption();

        return new CatletDrive
        {
            Id = driveData.Id ?? throw new InvalidOperationException(
                "The inventory data contains a drive without an ID."),
            Type = driveData.Type ?? CatletDriveType.Dvd,
            AttachedDisk = disk.IfNoneUnsafe(() => null),
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

    /// <remarks>
    /// The site is the one of the host reporting the disk. A disk is where the host that holds it is,
    /// so this is an observation, not a lookup: it is only used when the disk is first recorded and
    /// never re-derived for one that already exists.
    /// </remarks>
    protected async Task<Option<VirtualDisk>> AddOrUpdateDisk(
        string agentName,
        Guid siteId,
        DateTimeOffset timestamp,
        DiskInfo diskInfo)
    {
        var disk = await GetDisk(agentName, diskInfo);
        if (disk is not null && (disk.LastSeen >= timestamp || disk.Project.BeingDeleted))
            return disk;

        Option<VirtualDisk> parentDisk = null;
        if (diskInfo.Parent is not null)
            parentDisk = await AddOrUpdateDisk(agentName, siteId, timestamp, diskInfo.Parent);

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

        if (diskInfo.Name is null || diskInfo.DataStore is null || diskInfo.Environment is null)
            throw new InvalidOperationException(
                $"The inventory data for disk {diskInfo.Id} is missing the name, data store, or environment.");

        disk = new VirtualDisk
        {
            Id = diskInfo.Id,
            Name = diskInfo.Name,
            DiskIdentifier = diskInfo.DiskIdentifier,
            DataStore = diskInfo.DataStore,
            Environment = diskInfo.Environment,
            SiteId = siteId,
            StorageIdentifier = diskInfo.StorageIdentifier,
            Project = project,
            FileName = diskInfo.FileName,
            Path = diskInfo.Path?.ToLowerInvariant(),
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
                }).ToArray(),
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
                diskInfo.DataStore ?? "",
                diskInfo.Environment ?? "",
                // Gene-pool disks have no storage identifier and are persisted with
                // StorageIdentifier == null. Pass the nullable value through (instead of
                // coalescing to "") so EF emits "IS NULL" and matches the existing row;
                // otherwise every inventory run fails the lookup and inserts a duplicate.
                diskInfo.StorageIdentifier,
                diskInfo.Name ?? "",
                diskInfo.DiskIdentifier));

        return virtualDisks.Length() > 1
            ? virtualDisks.FirstOrDefault(d =>
                string.Equals(d.Path, diskInfo.Path, StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.FileName, diskInfo.FileName, StringComparison.OrdinalIgnoreCase))
            : virtualDisks.FirstOrDefault();
    }

    protected Seq<Guid> CollectDiskIdentifiers(Seq<DiskInfo> diskInfos) =>
        diskInfos.Map(d => Optional(d.Parent)).Somes()
            .Match(Seq<Guid>, CollectDiskIdentifiers)
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
