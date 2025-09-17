using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateCatletCommand : IHasCorrelationId, IHasResource
{
    public CatletConfig Config { get; set; }

    public Guid CorrelationId { get; set; }

    public Guid CatletId { get; set; }

    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
