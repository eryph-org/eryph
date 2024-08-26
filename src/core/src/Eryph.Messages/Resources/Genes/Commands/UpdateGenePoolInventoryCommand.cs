using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Resources.GenePool;

namespace Eryph.Messages.Resources.Genes.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateGenePoolInventoryCommand
{
    [PrivateIdentifier]
    public string AgentName { get; set; }

    public List<GeneSetData> Inventory { get; set; }
    
    public DateTimeOffset Timestamp { get; set; }
}
