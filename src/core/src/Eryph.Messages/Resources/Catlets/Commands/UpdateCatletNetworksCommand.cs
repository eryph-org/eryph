using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateCatletNetworksCommand: IHasResource, IHasProjectId
{
    public Guid ProjectId { get; set; }

    public CatletConfig Config { get; set; }
    
    public Guid CatletId { get; set; }

    public Guid CatletMetadataId { get; set; }

    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
