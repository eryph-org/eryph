using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class CreateCatletCommand : IHasCorrelationId, ICommandWithName
{
    public Guid TenantId { get; set; }

    public string Name { get; set; }

    public string ConfigYaml { get; set; }

    public Guid CorrelationId { get; set; }

    public string GetCommandName() => $"Create catlet {Name}";
}
