using System;
using System.ComponentModel.DataAnnotations;
using Eryph.Resources.Machines;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualMachineDrive
{
    [Key] public string Id { get; set; }

    public VirtualMachineDriveType? Type { get; set; }

    public Guid AttachedDiskId { get; set; }

}