using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class UpdateCatletConfigDriveCommand : IVMCommand, IHasResource
{
    public CatletConfig Config { get; set; }

    public Guid MetadataId { get; set; }

    public bool SecretDataHidden { get; set; }

    public Resource Resource => new(ResourceType.Catlet, CatletId);

    public Guid CatletId { get; set; }

    public Guid VmId { get; set; }
}
