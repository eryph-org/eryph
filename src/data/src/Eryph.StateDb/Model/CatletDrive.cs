using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.StateDb.Model;

public class CatletDrive
{
    /// <summary>
    /// The Hyper-V ID of the drive attached to the catlet VM.
    /// </summary>
    public required string Id { get; set; }

    public Guid CatletId { get; set; }

    public Catlet Catlet { get; set; } = null!;

    public CatletDriveType Type { get; set; }

    public Guid? AttachedDiskId { get; set; }

    /// <summary>
    /// Contains the actual virtual disk which is attached.
    /// </summary>
    /// <remarks>
    /// Can be <see langword="null"/> when e.g. the VHD has
    /// been deleted but is still attached to a VM in Hyper-V.
    /// </remarks>
    public VirtualDisk? AttachedDisk { get; set; }
}
