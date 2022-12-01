using System;
using System.ComponentModel.DataAnnotations;
using Eryph.ConfigModel.Machine;
using Eryph.Resources.Machines;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualCatletDrive
{
    [Key] public string Id { get; set; }

    public VirtualMachineDriveType? Type { get; set; }

    public Guid AttachedDiskId { get; set; }

}