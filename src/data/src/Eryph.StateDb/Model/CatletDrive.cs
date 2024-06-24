using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.StateDb.Model;

public class CatletDrive
{
    public required string Id { get; set; }

    public Guid CatletId { get; set; }

    public Catlet Catlet { get; set; } = null!;

    public CatletDriveType Type { get; set; }

    public Guid? AttachedDiskId { get; set; }

    public VirtualDisk? AttachedDisk { get; set; }
}
