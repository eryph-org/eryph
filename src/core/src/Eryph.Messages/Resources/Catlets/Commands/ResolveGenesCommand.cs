using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class ResolveGenesCommand : IHostAgentCommand
{
    public string AgentName { get; set; }

    public IReadOnlyList<GeneIdentifier> Genes { get; set; }
}
