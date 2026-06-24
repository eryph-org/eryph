using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class ExpandNewCatletConfigCommand : IHasCorrelationId
{
    public CatletConfig? Config { get; set; }

    public bool ShowSecrets { get; set; }

    public Guid CorrelationId { get; set; }
}
