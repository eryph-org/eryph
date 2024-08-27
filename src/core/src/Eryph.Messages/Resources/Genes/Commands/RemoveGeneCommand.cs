using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Resources.Genes.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class RemoveGeneCommand : IHasCorrelationId
{
    public Guid CorrelationId { get; set; }

    public Guid Id { get; set; }
}
