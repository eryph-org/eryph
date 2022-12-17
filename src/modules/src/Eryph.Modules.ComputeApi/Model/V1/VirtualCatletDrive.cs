using System;
using System.ComponentModel.DataAnnotations;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualCatletDrive
{
    [Key] public string Id { get; set; }

    public VirtualCatletDriveType? Type { get; set; }

    public Guid AttachedDiskId { get; set; }

}