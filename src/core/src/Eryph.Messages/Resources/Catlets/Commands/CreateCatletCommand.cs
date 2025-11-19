using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class CreateCatletCommand : IHasCorrelationId, ICommandWithName
{
    public Guid TenantId { get; set; }

    public string Name { get; set; }

    public CatletConfig Config { get; set; }

    public Guid CorrelationId { get; set; }

    public string GetCommandName() => $"Create catlet {Name}";
}
