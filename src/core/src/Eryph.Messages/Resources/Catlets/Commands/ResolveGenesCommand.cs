using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class ResolveGenesCommand : IHostAgentCommand
{
    public string AgentName { get; set; }

    public GeneArchitecture CatletArchitecture { get; set; }

    public IReadOnlyList<GeneIdentifierWithType> Genes { get; set; }
}
