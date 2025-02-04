using Eryph.ConfigModel.Catlets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class PrepareNewCatletConfigCommand : IHasCorrelationId, ICommandWithName
{
    public Guid TenantId { get; set; }

    public CatletConfig Config { get; set; }

    public Guid CorrelationId { get; set; }

    public string GetCommandName()
    {
        var catletName = Config?.Name ?? "Catlet";
        return $"Prepare config for new catlet {catletName}";
    }
}
