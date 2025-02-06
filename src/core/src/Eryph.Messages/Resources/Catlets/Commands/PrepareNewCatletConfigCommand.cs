using Eryph.ConfigModel.Catlets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class PrepareNewCatletConfigCommand : IHasCorrelationId
{
    public CatletConfig Config { get; set; }

    public Guid CorrelationId { get; set; }
}
