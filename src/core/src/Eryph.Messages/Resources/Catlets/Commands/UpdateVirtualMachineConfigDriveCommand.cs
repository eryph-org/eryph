using System;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class UpdateVirtualCatletConfigDriveCommand : IVMCommand
{
    public Guid CatletId { get; set; }
    public Guid VMId { get; set; }

    public VirtualCatletMetadata MachineMetadata { get; set; }
    public MachineNetworkSettings[] MachineNetworkSettings { get; set; }

}