using System;
using System.Collections.Generic;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Resources.Disks;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualDisk
{
    public required string Id { get; set; }

    public required string Name { get; set; }
        
    public required string Location { get; set; }
        
    public required string DataStore { get; set; }
        
    public required Project Project { get; set; }
        
    public required string Environment { get; set; }

    /// <summary>
    /// The status of the disk. The status <c>Error</c>
    /// indicates that Hyper-V considers the disk to be unusable,
    /// i.e. the disk has failed <c>Test-VHD</c>.
    /// </summary>
    public required DiskStatus Status { get; set; }

    public VirtualDiskGeneInfo? Gene { get; set; }

    /// <summary>
    /// The file system path of the virtual disk. This information
    /// is only available to administrators.
    /// </summary>
    public string? Path { get; set; }

    public long? SizeBytes { get; set; }

    /// <summary>
    /// The ID of the parent disk when this disk is a differential disk.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// The file system path of the virtual disk's parent. This information
    /// is only available to administrators. The ParentPath might be populated
    /// even if the ParentId is missing. In this case, the disk chain is corrupted.
    /// </summary>
    public string? ParentPath { get; set; }

    public IReadOnlyList<VirtualDiskAttachedCatlet>? AttachedCatlets { get; set; }
}
