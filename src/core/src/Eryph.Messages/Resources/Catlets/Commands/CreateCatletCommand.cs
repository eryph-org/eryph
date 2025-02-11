using System;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class CreateCatletCommand : IHasCorrelationId, ICommandWithName
{
    public Guid TenantId { get; set; }

    public CatletConfig Config { get; set; }

    public Guid CorrelationId { get; set; }

    public string GetCommandName()
    {
        var catletName = string.IsNullOrWhiteSpace(Config.Name)
            ? EryphConstants.DefaultCatletName
            : Config.Name;
        return $"Create catlet {catletName}";
    }
}
