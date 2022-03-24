using System;
using Eryph.Core;
using Eryph.Resources.Machines;
using Eryph.Resources.Machines.Config;

namespace Eryph.Messages.Resources.Machines.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class UpdateVirtualMachineConfigDriveCommand : IVMCommand
{
    public Guid MachineId { get; set; }
    public Guid VMId { get; set; }

    public VirtualMachineMetadata MachineMetadata { get; set; }


}