using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Genes.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class RemoveGenesVMCommand : IHostAgentCommand
{
    public List<GeneIdentifierWithType> Genes { get; set; }

    [PrivateIdentifier]
    public string AgentName { get; set; }
}
