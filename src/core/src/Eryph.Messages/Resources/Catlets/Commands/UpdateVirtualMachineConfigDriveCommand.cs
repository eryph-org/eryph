using System;
using Eryph.Resources;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class UpdateVirtualCatletConfigDriveCommand : IVMCommand, IHasResource
{
    public Guid CatletId { get; set; }
    public Guid VMId { get; set; }

    public VirtualCatletMetadata MachineMetadata { get; set; }
    public MachineNetworkSettings[] MachineNetworkSettings { get; set; }

    public Resource Resource => new(ResourceType.Catlet, CatletId);
}