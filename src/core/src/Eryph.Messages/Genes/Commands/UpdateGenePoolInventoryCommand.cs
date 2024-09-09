using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.GenePool;

namespace Eryph.Messages.Genes.Commands;

/// <summary>
/// This command tells the controller to update the gene pool
/// inventory for the agent with the given <see cref="AgentName"/>.
/// The <see cref="Inventory"/> must contain a complete list of the
/// genes in the agent's gene pool. The controller is expected to
/// remove any missing genes from the inventory.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateGenePoolInventoryCommand
{
    [PrivateIdentifier]
    public string AgentName { get; set; }

    public List<GeneData> Inventory { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
