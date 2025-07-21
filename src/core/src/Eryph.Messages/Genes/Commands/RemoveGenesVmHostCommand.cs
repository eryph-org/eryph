using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Messages.Resources;

namespace Eryph.Messages.Genes.Commands;

[SendMessageTo(MessageRecipient.GenepoolAgent)]
public class RemoveGenesVmHostCommand : IGenepoolAgentCommand
{
    public IReadOnlyList<UniqueGeneIdentifier> Genes { get; set; }

    [PrivateIdentifier]
    public string AgentName { get; set; }
}
