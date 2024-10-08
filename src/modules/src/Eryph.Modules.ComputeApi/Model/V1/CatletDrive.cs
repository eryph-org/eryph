using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletDrive
{
    public required CatletDriveType Type { get; set; }

    /// <summary>
    /// The ID of the actual virtual disk which is attached.
    /// This can be null, e.g. when the VHD has been deleted,
    /// but it is still configured in the virtual machine.
    /// </summary>
    public string? AttachedDiskId { get; set; }
}
