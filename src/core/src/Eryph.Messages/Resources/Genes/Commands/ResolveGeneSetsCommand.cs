using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Genes.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class ResolveGeneSetsCommand : IHostAgentCommand
{
    public CatletConfig Config { get; set; }

    [PrivateIdentifier]
    public string AgentName { get; set; }
}