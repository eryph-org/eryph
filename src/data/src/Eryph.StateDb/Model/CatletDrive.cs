using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.StateDb.Model;

public class CatletDrive
{
    public string Id { get; set; } = null!;

    public Guid CatletId { get; set; }

    public virtual Catlet Catlet { get; set; } = null!;

    public CatletDriveType Type { get; set; }

    public Guid? AttachedDiskId { get; set; }

    public virtual VirtualDisk? AttachedDisk { get; set; }
}
