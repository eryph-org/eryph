using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class PrepareNewCatletConfigCommand : IHasCorrelationId
{
    public CatletConfig? Config { get; set; }

    public Guid CorrelationId { get; set; }
}
