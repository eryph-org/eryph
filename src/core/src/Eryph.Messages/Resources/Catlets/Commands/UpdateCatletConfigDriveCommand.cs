using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources;
using Eryph.Resources.Machines;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class UpdateCatletConfigDriveCommand : IVMCommand, IHasResource
{
    public CatletConfig Config { get; set; }

    public Guid CatletId { get; set; }

    public Guid VMId { get; set; }

    public CatletMetadata MachineMetadata { get; set; }
    
    // TODO is this used?
    public MachineNetworkSettings[] MachineNetworkSettings { get; set; }

    public Resource Resource => new(ResourceType.Catlet, CatletId);
}