using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class ExpandCatletConfigCommand : IHasCorrelationId, IHasResource
{
    public Guid CatletId { get; set; }

    public CatletConfig Config { get; set; }

    public Guid CorrelationId { get; set; }

    public Resource Resource => new(ResourceType.Catlet, CatletId);
}