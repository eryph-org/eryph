using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.StateDb.Model;

public class VirtualDisk : Disk
{
    public VirtualDisk()
    {
        ResourceType = ResourceType.VirtualDisk;
    }

    public string? StorageIdentifier { get; set; }
    
    public Guid DiskIdentifier { get; set; }

    public bool Frozen { get; set; }

    /// <summary>
    /// Indicates that the disk has been deleted. Disk are not
    /// directly removed from the database but marked as deleted.
    /// Otherwise, the inventory might add deleted disks again in
    /// some corner cases.
    /// </summary>
    public bool Deleted { get; set; }

    public string? Path { get; set; }
        
    public string? FileName { get; set; }

    public long? SizeBytes { get; set; }
    
    public long? UsedSizeBytes { get; set; }

    public ICollection<VirtualDisk> Children { get; set; } = null!;

    public ICollection<CatletDrive> AttachedDrives { get; set; } = null!;

    public Guid? ParentId { get; set; }
    
    public VirtualDisk? Parent { get; set; }
    
    public DateTimeOffset LastSeen { get; set; }
    
    public string? LastSeenAgent { get; set; }

    /// <summary>
    /// This property and <see cref="GeneName"/> and <see cref="GeneArchitecture"/>
    /// are only populated when this disk is part of the gene pool.
    /// </summary>
    public string? GeneSet { get; set; }

    /// <summary>
    /// This property and <see cref="GeneSet"/> and <see cref="GeneArchitecture"/>
    /// are only populated when this disk is part of the gene pool.
    /// </summary>
    public string? GeneName { get; set; }

    /// <summary>
    /// This property and <see cref="GeneSet"/> and <see cref="GeneName"/>
    /// are only populated when this disk is part of the gene pool.
    /// </summary>
    public string? GeneArchitecture { get; set; }

    /// <summary>
    /// This property is only used optimize database queries
    /// and should not be used directly outside of queries.
    /// </summary>
    internal string? UniqueGeneIndex
    {
        get => StateStoreGeneExtensions.ToUniqueGeneIndex(GeneSet, GeneName, GeneArchitecture);
        // The setter is only defined so EF Core persists the property to the
        // database (for indexing). It does not update the property.
#pragma warning disable S1144
        private set => _ = value;
#pragma warning restore S1144
    }
}
