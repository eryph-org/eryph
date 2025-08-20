using System;
using System.Collections.Generic;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
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

    public Resource Resource => new(ResourceType.Catlet, CatletId);
}