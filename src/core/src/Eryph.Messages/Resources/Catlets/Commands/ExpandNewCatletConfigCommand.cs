using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class ExpandNewCatletConfigCommand : IHasCorrelationId
{
    public Guid TenantId { get; set; }

    public CatletConfig Config { get; set; }

    public Guid CorrelationId { get; set; }
}
