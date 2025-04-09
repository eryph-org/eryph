using Eryph.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class KillVMCommand : IVMCommand, IHasResource
{
    public Guid CatletId { get; set; }

    public Guid VMId { get; set; }

    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
