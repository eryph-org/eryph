﻿using System;
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

    public string? Geneset { get; set; }

    public bool Frozen { get; set; }

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
}
