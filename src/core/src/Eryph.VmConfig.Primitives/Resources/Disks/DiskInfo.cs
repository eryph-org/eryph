using System;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using JetBrains.Annotations;

namespace Eryph.Resources.Disks;

public class DiskInfo
{
    public Guid Id { get; set; }

    public string Name { get; set; }
    
    public string StorageIdentifier { get; set; }
    
    public string DataStore { get; set; }

    public Guid? ProjectId { get; set; }
    
    public string ProjectName { get; set; }
    
    public string Environment { get; set; }

    public Guid DiskIdentifier { get; set; }

    [CanBeNull] public UniqueGeneIdentifier Gene { get; set; }

    public bool Frozen { get; set; }

    public DiskStatus Status { get; set; }

    [PrivateIdentifier] public string Path { get; set; }

    [PrivateIdentifier] public string FileName { get; set; }

    public long? SizeBytes { get; set; }
    public long? UsedSizeBytes { get; set; }

    [CanBeNull] public DiskInfo Parent { get; set; }

    /// <summary>
    /// The path to the parent of this disk. The parent path might be
    /// populated even if <see cref="Parent"/> is <see langword="null"/>.
    /// This means that this disk is differential (i.e. it has parent) but
    /// the parent is missing.
    /// </summary>
    [CanBeNull] public string ParentPath { get; set; }
}