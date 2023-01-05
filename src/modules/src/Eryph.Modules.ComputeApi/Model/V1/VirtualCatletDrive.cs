using System;
using System.ComponentModel.DataAnnotations;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualCatletDrive
{

    public VirtualCatletDriveType? Type { get; set; }

    public Guid AttachedDiskId { get; set; }

}