using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class ValidateCatletNetworksCommand : ICommandWithName
{
    public Guid TenantId { get; set; }

    public CatletConfig Config { get; set; }

    public string GetCommandName()
    {
        var catletName = string.IsNullOrWhiteSpace(Config.Name)
            ? EryphConstants.DefaultCatletName
            : Config.Name;
        return $"Validating network for catlet {catletName}";
    }
}
