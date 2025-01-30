using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Resources;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class SyncVmNetworkPortsCommand : IVMCommand, IHasResource
{
    public Guid CatletId { get; set; }

    public Guid VMId { get; set; }
    
    public Resource Resource => new(ResourceType.Catlet, CatletId);
}
