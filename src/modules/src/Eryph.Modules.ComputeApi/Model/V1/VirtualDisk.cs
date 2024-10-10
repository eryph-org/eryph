using System;
using System.Collections.Generic;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

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
    /// The file system path of the virtual disk. This information
    /// is only available to administrators.
    /// </summary>
    public string? Path { get; set; }

    public long? SizeBytes { get; set; }

    /// <summary>
    /// The ID of the parent disk when this disk is a differential disk.
    /// </summary>
    public string? ParentId { get; set; }

    public IReadOnlyList<VirtualDiskAttachedCatlet>? AttachedCatlets { get; set; }
}
