using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.GenePool;

namespace Eryph.Messages.Genes.Commands;

/// <summary>
/// This command tells the controller to update the inventory
/// information only for the genes included in the
/// <see cref="Inventory"/>. In difference to the
/// <see cref="UpdateGenePoolInventoryCommand"/>, the controller
/// is not expected to remove any genes from the inventory.
/// </summary>
[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateGenesInventoryCommand
{
    [PrivateIdentifier]
    public string AgentName { get; set; }

    public IReadOnlyList<GeneData> Inventory { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
